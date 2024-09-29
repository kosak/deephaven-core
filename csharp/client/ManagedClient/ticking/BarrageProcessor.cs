using Google.FlatBuffers;
using io.deephaven.barrage.flatbuf;

namespace Deephaven.ManagedClient;

interface IChunkProcessor {
  TickingUpdate? ProcessNextChunk(IColumnSource[] sources, int[] sizes, byte[] metadata);
}

public class BarrageProcessor {
  private IChunkProcessor _currentProcessor;

  public BarrageProcessor() {
    _currentProcessor = new AwaitingMetadata();
  }

  public const UInt32 DeephavenMagicNumber = 0x6E687064U;

  public static byte[] CreateSubscriptionRequest(byte[] ticketBytes, int size) {
    throw new NotImplementedException("NIY");
  }

  public TickingUpdate? ProcessNextChunk(IColumnSource[] sources, int[] sizes, byte[] metadata) {
    return _currentProcessor.ProcessNextChunk(sources, sizes, metadata);
  }
}

class AwaitingMetadata : IChunkProcessor {
  public TickingUpdate? ProcessNextChunk(IColumnSource[] sources, int[] sizes, byte[] metadata) {
    if (metadata == null) {
      throw new Exception("Metadata was required here, but none was received");
    }

    var bmw = BarrageMessageWrapper.GetRootAsBarrageMessageWrapper(new ByteBuffer(metadata));

    if (bmw.Magic != BarrageProcessor.DeephavenMagicNumber) {
      throw new Exception($"Expected magic number {BarrageProcessor.DeephavenMagicNumber:x}, got {bmw.Magic:x}");
    }

    if (bmw.MsgType != BarrageMessageType.BarrageUpdateMetadata) {
      throw new Exception($"Expected Barrage Message Type {BarrageMessageType.BarrageUpdateMetadata}, got {bmw.MsgType}");
    }

    // var payloadRawSbytes = bmw.GetMsgPayloadArray();
    // var payloadRawBytes = new byte[payloadRawSbytes.Length];
    // Array.Copy(payloadRawSbytes, payloadRawBytes, payloadRawSbytes.Length);
    var bytes = bmw.GetMsgPayloadBytes()!.ToArray<byte>();
    var bmd = BarrageUpdateMetadata.GetRootAsBarrageUpdateMetadata(new ByteBuffer(bytes));

    var diRemoved = new DataInput(bmd.GetRemovedRowsBytes()!);
    var diThreeShiftIndices = new DataInput(bmd.GetShiftDataBytes()!);
    var diAdded = new DataInput(bmd.GetAddedRowsBytes()!);

    var removedRows = IndexDecoder.ReadExternalCompressedDelta(diRemoved);
    var shiftStartIndex = IndexDecoder.ReadExternalCompressedDelta(diThreeShiftIndices);
    var shiftEndIndex = IndexDecoder.ReadExternalCompressedDelta(diThreeShiftIndices);
    var shiftDestIndex = IndexDecoder.ReadExternalCompressedDelta(diThreeShiftIndices);
    var addedRows = IndexDecoder.ReadExternalCompressedDelta(diAdded);
    return null;
  }
}