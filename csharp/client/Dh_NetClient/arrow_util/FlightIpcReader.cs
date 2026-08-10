//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Deephaven.Dh_NetClient.ArrowFlightLite;
using Grpc.Core;

namespace Deephaven.Dh_NetClient;

/// <summary>
/// Reads Arrow record batches from a raw stream of Flight FlightData messages.
///
/// This exists because the Apache.Arrow.Flight package's typed readers do not implement
/// DictionaryBatch IPC messages, so a stream carrying dictionary-encoded columns kills the
/// subscription with NotImplementedException. The core Apache.Arrow ArrowStreamReader, by
/// contrast, handles dictionary batches fully (including isDelta deltas), so this class
/// re-frames each FlightData as an Arrow IPC stream message (a FlightData's data_header and
/// data_body are exactly an IPC message minus its length prefix) and delegates all decoding
/// to an ArrowStreamReader.
///
/// ReadNextRecordBatchAsync returns only record batches; schema and dictionary messages are
/// consumed internally. App metadata that arrives on or before a record batch is collected
/// and can be retrieved with TakeAppMetadata after each read.
/// </summary>
internal sealed class FlightIpcReader : IDisposable {
  private readonly FramingStream _framingStream;
  private readonly ArrowStreamReader _reader;

  public FlightIpcReader(IAsyncStreamReader<FlightData> flightDataStream) {
    _framingStream = new FramingStream(flightDataStream);
    _reader = new ArrowStreamReader(_framingStream);
  }

  public async ValueTask<RecordBatch?> ReadNextRecordBatchAsync(
    CancellationToken cancellationToken = default) {
    NormalizeEmptyDictionaries();
    return await _reader.ReadNextRecordBatchAsync(cancellationToken);
  }

  // Reflection handles into ArrowStreamReader's dictionary bookkeeping, used by
  // NormalizeEmptyDictionaries below. Null if the library's internals ever change shape.
  private static readonly FieldInfo? ImplementationField =
    typeof(ArrowStreamReader).GetField("_implementation", BindingFlags.Instance | BindingFlags.NonPublic);
  private static readonly FieldInfo? DictionaryMemoField =
    ImplementationField?.FieldType.GetField("_dictionaryMemo", BindingFlags.Instance | BindingFlags.NonPublic);
  private static readonly FieldInfo? IdToDictionaryField =
    DictionaryMemoField?.FieldType.GetField("_idToDictionary", BindingFlags.Instance | BindingFlags.NonPublic);

  /// <summary>
  /// Works around an Apache.Arrow bug: a zero-row dictionary deserialized from the wire has
  /// zero-length buffers (in particular, a variable-width array arrives with no offsets
  /// buffer), and ArrowArrayConcatenator throws ArgumentOutOfRangeException when a later
  /// isDelta DictionaryBatch is concatenated onto it. Barrage hits this whenever a
  /// subscription's initial snapshot is empty (a fresh time table, say): the snapshot carries
  /// an empty base dictionary and the first real update carries a delta. This rewrites any
  /// empty dictionary stored in the reader's memo into a well-formed empty array before the
  /// next read can merge into it. If the reflection handles are unavailable, this does
  /// nothing, and such streams fail as the library bug dictates.
  /// </summary>
  private void NormalizeEmptyDictionaries() {
    var implementation = ImplementationField?.GetValue(_reader);
    var memo = implementation == null ? null : DictionaryMemoField?.GetValue(implementation);
    var idToDictionary = memo == null ? null : (IDictionary?)IdToDictionaryField?.GetValue(memo);
    if (idToDictionary == null || idToDictionary.Count == 0) {
      return;
    }

    List<(object Key, IArrowArray Replacement)>? replacements = null;
    foreach (DictionaryEntry entry in idToDictionary) {
      if (entry.Value is not IArrowArray { Length: 0 } array) {
        continue;
      }
      var replacement = EncodedArrayDecoder.TryMakeWellFormedEmpty(array);
      if (replacement != null) {
        (replacements ??= []).Add((entry.Key, replacement));
      }
    }

    if (replacements == null) {
      return;
    }
    foreach (var (key, replacement) in replacements) {
      idToDictionary[key] = replacement;
    }
  }

  /// <summary>
  /// The stream's schema. Only safe to access after the first ReadNextRecordBatchAsync call
  /// (which consumes the schema message even when it returns null for an empty stream).
  /// </summary>
  public Schema Schema => _reader.Schema;

  /// <summary>
  /// Returns the app_metadata payloads of the FlightData messages consumed since the last call,
  /// which for a call made right after ReadNextRecordBatchAsync means the metadata accompanying
  /// that record batch (and any schema/dictionary messages that preceded it).
  /// </summary>
  public List<byte[]> TakeAppMetadata() {
    var result = _framingStream.PendingAppMetadata;
    _framingStream.PendingAppMetadata = [];
    return result;
  }

  public void Dispose() {
    _reader.Dispose();
  }

  /// <summary>
  /// Adapts the FlightData messages to the Arrow IPC stream format: each message is
  /// [0xFFFFFFFF continuation] [int32 length of padded header] [data_header, zero-padded
  /// to 8 bytes] [data_body]. gRPC stream end reads as end-of-stream (zero bytes).
  /// </summary>
  private sealed class FramingStream : Stream {
    private readonly IAsyncStreamReader<FlightData> _source;
    private byte[] _current = [];
    private int _pos;
    private bool _exhausted;

    public List<byte[]> PendingAppMetadata = [];

    public FramingStream(IAsyncStreamReader<FlightData> source) {
      _source = source;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
      CancellationToken cancellationToken = default) {
      while (_pos == _current.Length) {
        if (_exhausted || !await _source.MoveNext(cancellationToken)) {
          _exhausted = true;
          return 0;
        }
        var flightData = _source.Current;
        if (flightData.AppMetadata.Length > 0) {
          PendingAppMetadata.Add(flightData.AppMetadata.ToByteArray());
        }
        // A message with no data_header (e.g. metadata-only) frames to zero bytes; keep looping.
        _current = FrameMessage(flightData);
        _pos = 0;
      }

      var numBytes = Math.Min(buffer.Length, _current.Length - _pos);
      _current.AsSpan(_pos, numBytes).CopyTo(buffer.Span);
      _pos += numBytes;
      return numBytes;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
      CancellationToken cancellationToken) {
      return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
    }

    public override int Read(byte[] buffer, int offset, int count) {
      return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
    }

    private static byte[] FrameMessage(FlightData flightData) {
      var header = flightData.DataHeader;
      if (header.Length == 0) {
        return [];
      }
      // The length field counts the header plus its zero padding, so the body that follows
      // stays 8-byte aligned after the 8-byte [continuation, length] prefix.
      var paddedHeaderLength = (header.Length + 7) & ~7;
      var body = flightData.DataBody;
      var frame = new byte[8 + paddedHeaderLength + body.Length];
      BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(0, 4), 0xFFFFFFFF);
      BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4, 4), paddedHeaderLength);
      header.CopyTo(frame, 8);
      body.CopyTo(frame, 8 + paddedHeaderLength);
      return frame;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  }
}
