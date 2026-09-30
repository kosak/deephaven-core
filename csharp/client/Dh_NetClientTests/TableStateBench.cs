//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics;
using Apache.Arrow;
using Apache.Arrow.Types;
using Deephaven.Dh_NetClient;

namespace Deephaven.Dh_NetClientTests;

/// <summary>
/// Times the client-side snapshot assembly with no server and no network involved, so that the cost of
/// TableState/SpaceMapper can be attributed independently of Barrage.
///
/// Feeds a table of DH_BENCH_ROWS rows to TableState in chunks of DH_BENCH_CHUNK, the way AwaitingAdds does
/// while a snapshot fills, and reports the time per phase. Skipped unless DH_BENCH_ROWS is set.
/// </summary>
public class TableStateBench {
  [Test]
  public void AddDataInChunks() {
    var rowsEnv = Environment.GetEnvironmentVariable("DH_BENCH_ROWS");
    if (string.IsNullOrEmpty(rowsEnv)) {
      Console.Error.WriteLine("DH_BENCH_ROWS not set; skipping.");
      return;
    }

    var totalRows = int.Parse(rowsEnv);
    var chunkSize = int.TryParse(Environment.GetEnvironmentVariable("DH_BENCH_CHUNK"), out var cs) ? cs : 4096;
    var snapshotsPerChunk =
      int.TryParse(Environment.GetEnvironmentVariable("DH_BENCH_SNAPSHOTS"), out var sp) ? sp : 0;

    var outPath = Environment.GetEnvironmentVariable("DH_BENCH_OUT");
    using var sink = outPath is null
      ? (TextWriter)Console.Error
      : new StreamWriter(outPath, append: true) { AutoFlush = true };

    // Six columns, matching the shape of the table used in the barrage measurement.
    var schema = new Schema.Builder()
      .Field(f => f.Name("Sym").DataType(StringType.Default))
      .Field(f => f.Name("Timestamp").DataType(new TimestampType(TimeUnit.Nanosecond, (string?)null)))
      .Field(f => f.Name("Price").DataType(DoubleType.Default))
      .Field(f => f.Name("Size").DataType(Int32Type.Default))
      .Field(f => f.Name("Venue").DataType(StringType.Default))
      .Field(f => f.Name("Seq").DataType(Int64Type.Default))
      .Build();

    var tableState = new TableState(schema);

    // One chunk's worth of source data, reused for every chunk: we are timing the insert, not the decode.
    var sources = MakeSources(schema, chunkSize);

    var addKeysMs = 0.0;
    var addDataMs = 0.0;
    var snapshotMs = 0.0;
    var sw = new Stopwatch();

    var nChunks = 0;
    for (var start = 0; start < totalRows; start += chunkSize) {
      var thisChunk = Math.Min(chunkSize, totalRows - start);
      var keys = RowSequence.CreateSequential(Interval.OfStartAndSize((ulong)start, (ulong)thisChunk));

      sw.Restart();
      var indexSpace = tableState.AddKeysAllowingExisting(keys);
      addKeysMs += sw.Elapsed.TotalMilliseconds;

      var sar = new SourceAndRange[sources.Length];
      for (var i = 0; i != sources.Length; ++i) {
        sar[i] = new SourceAndRange(sources[i], Interval.OfStartAndSize(0, (ulong)thisChunk));
      }

      sw.Restart();
      tableState.AddData(sar, indexSpace);
      addDataMs += sw.Elapsed.TotalMilliseconds;

      for (var s = 0; s != snapshotsPerChunk; ++s) {
        sw.Restart();
        _ = tableState.Snapshot();
        snapshotMs += sw.Elapsed.TotalMilliseconds;
      }

      ++nChunks;
    }

    var total = addKeysMs + addDataMs + snapshotMs;
    sink.WriteLine($"=== rows={totalRows:N0} chunk={chunkSize:N0} chunks={nChunks:N0} " +
                   $"snapshots/chunk={snapshotsPerChunk}");
    sink.WriteLine($"  AddKeysAllowingExisting : {addKeysMs,10:N0} ms");
    sink.WriteLine($"  AddData                 : {addDataMs,10:N0} ms");
    sink.WriteLine($"  Snapshot                : {snapshotMs,10:N0} ms");
    sink.WriteLine($"  TOTAL                   : {total,10:N0} ms");
  }

  private static ArrayColumnSource[] MakeSources(Schema schema, int n) {
    var result = new ArrayColumnSource[schema.FieldsList.Count];
    for (var i = 0; i != result.Length; ++i) {
      var src = ArrayColumnSource.CreateFromArrowType(schema.FieldsList[i].DataType, n);
      result[i] = src;
    }
    return result;
  }
}
