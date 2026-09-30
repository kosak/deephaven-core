//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics;
using Deephaven.Dh_NetClient;

namespace Deephaven.Dh_NetClientTests;

/// <summary>
/// Measures how quickly a large ticking table's initial snapshot is delivered, one line per update.
///
/// Exists because a subscription to a ~140k-row table was observed taking 25 seconds to fill, arriving in eight
/// batches with three-to-five second gaps between them. The mirror applies each batch in milliseconds, so the
/// gaps are the server pacing the snapshot rather than anything on the client side. This prints the gap and the
/// running row count so the effect of the subscription options can be compared:
///
///   DH_BARRAGE_BATCH_SIZE        rows per batch to request (0 = server decides; default 4096)
///   DH_BARRAGE_MAX_MESSAGE_SIZE  byte budget per message (0 = server default)
///
/// Both are passed to TableHandle.Subscribe, so nothing about the library's own defaults changes.
///
/// Set DH_TABLE to the table to subscribe to. Skipped when it is unset.
/// </summary>
public class BarrageBatchSizeSweep {
  [Test]
  public async Task MeasureSnapshotDelivery() {
    var tableName = Environment.GetEnvironmentVariable("DH_TABLE");
    if (string.IsNullOrEmpty(tableName)) {
      Console.Error.WriteLine("DH_TABLE not set; skipping.");
      return;
    }

    // Passed to Subscribe rather than read by the library, so the options under test stay a property of the
    // test and the client keeps its defaults.
    var batchSize = int.TryParse(Environment.GetEnvironmentVariable("DH_BARRAGE_BATCH_SIZE"), out var bs)
      ? bs
      : BarrageProcessor.DefaultBatchSize;
    var maxMessageSize = int.TryParse(Environment.GetEnvironmentVariable("DH_BARRAGE_MAX_MESSAGE_SIZE"), out var mms)
      ? mms
      : 0;

    var outPath = Environment.GetEnvironmentVariable("DH_SWEEP_OUT");
    using var sink = outPath is null
      ? (TextWriter)Console.Error
      : new StreamWriter(outPath, append: true) { AutoFlush = true };

    using var ctx = CommonContextForTests.Create(new ClientOptions());
    using var table = ctx.Client.Manager.FetchTable(tableName);

    sink.WriteLine(
      $"=== {tableName}: {table.NumRows} rows at open, batch_size={batchSize}, max_message_size={maxMessageSize}");

    var callback = new TimedSnapshotCallback(table.NumRows, sink);
    using var cookie = table.Subscribe(callback, batchSize, maxMessageSize);

    // Long enough to cover a slow fill, but the callback ends the wait as soon as the row count stops growing.
    await callback.WaitForFillAsync(TimeSpan.FromMinutes(2));

    sink.WriteLine(callback.Summary());
  }
}

/// <summary>
/// Records the arrival time and row count of every update, and considers the snapshot filled once the row count
/// has stopped growing for a couple of seconds.
/// </summary>
public sealed class TimedSnapshotCallback : IObserver<TickingUpdate> {
  private readonly long _rowsAtOpen;
  private readonly Stopwatch _sinceSubscribed = Stopwatch.StartNew();
  private readonly List<(int Seq, double GapMs, double ElapsedMs, long Added, long RowCount)> _updates = new();
  private readonly TaskCompletionSource _filled = new(TaskCreationOptions.RunContinuationsAsynchronously);

  private double _lastAtMs;
  private long _lastRowCount = -1;
  private DateTime _lastGrowthUtc = DateTime.UtcNow;

  private readonly TextWriter _sink;

  public TimedSnapshotCallback(long rowsAtOpen, TextWriter sink) {
    _rowsAtOpen = rowsAtOpen;
    _sink = sink;
  }

  public void OnNext(TickingUpdate update) {
    var nowMs = _sinceSubscribed.Elapsed.TotalMilliseconds;
    var rowCount = update.Current.NumRows;
    var added = (long)update.AddedRowsIndexSpace.Count;

    lock (_updates) {
      _updates.Add((_updates.Count + 1, nowMs - _lastAtMs, nowMs, added, rowCount));
      _lastAtMs = nowMs;

      _sink.WriteLine(
        $"  update #{_updates.Count,-3} +{nowMs - (_updates.Count > 1 ? _updates[^2].ElapsedMs : 0),8:N0}ms" +
        $"  t={nowMs,9:N0}ms  added={added,8:N0}  rows={rowCount,9:N0}");

      if (rowCount > _lastRowCount) {
        _lastRowCount = rowCount;
        _lastGrowthUtc = DateTime.UtcNow;
      }
    }

    // The snapshot is in once the table has reached the size it reported at open and stopped growing. A ticking
    // table keeps producing small modify-only updates after that, which are not what this measures.
    if (rowCount >= _rowsAtOpen && DateTime.UtcNow - _lastGrowthUtc > TimeSpan.FromSeconds(2)) {
      _filled.TrySetResult();
    }
  }

  public void OnError(Exception error) {
    _sink.WriteLine($"  FAILED: {error.Message}");
    _filled.TrySetResult();
  }

  public void OnCompleted() {
  }

  public async Task WaitForFillAsync(TimeSpan timeout) {
    await Task.WhenAny(_filled.Task, Task.Delay(timeout));
  }

  public string Summary() {
    lock (_updates) {
      if (_updates.Count == 0) {
        return "  no updates received";
      }

      var last = _updates[^1];
      var biggestGap = _updates.Max(u => u.GapMs);

      return $"  => {last.RowCount:N0} rows over {_updates.Count} updates in {last.ElapsedMs:N0}ms" +
             $" (largest gap {biggestGap:N0}ms)";
    }
  }
}
