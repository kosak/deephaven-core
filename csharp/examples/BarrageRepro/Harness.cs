//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics;
using Deephaven.Dh_NetClient;

namespace Deephaven.BarrageRepro;

/// <summary>
/// Connects to a server, defines a table with a Python snippet, subscribes, and reports what happened.
///
/// Every repro in this project has the same shape: build a table on the server, subscribe to it, and watch
/// either an exception arrive at <see cref="IObserver{T}.OnError"/> or the fill take far longer than the data
/// volume justifies. This holds that shared part so each repro file is just its table definition and its
/// expectation.
/// </summary>
internal static class Harness {
  /// <summary>
  /// Host and port come from DH_HOST / DH_PORT, defaulting to a local anonymous server.
  /// </summary>
  public static Client Connect() {
    var host = Environment.GetEnvironmentVariable("DH_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("DH_PORT") ?? "10000";
    Console.WriteLine($"Connecting to {host}:{port}");
    return Client.Connect($"{host}:{port}", new ClientOptions().SetSessionType("python"));
  }

  /// <summary>
  /// Runs <paramref name="script"/> on the server, subscribes to <paramref name="tableName"/>, and waits for
  /// the subscription either to deliver the whole table or to fail.
  /// </summary>
  /// <returns>The outcome of the subscription: how long it took, how many messages, and any exception.</returns>
  public static Result Subscribe(
      Client client,
      string tableName,
      string script,
      TimeSpan timeout,
      bool verbose = true) {
    var manager = client.Manager;
    manager.RunScript(script);

    using var table = manager.FetchTable(tableName);
    var rowsAtOpen = table.NumRows;
    Console.WriteLine($"  {tableName}: {rowsAtOpen:N0} rows at subscribe time");

    var observer = new Observer(rowsAtOpen, verbose);
    var stopwatch = Stopwatch.StartNew();
    using (table.Subscribe(observer)) {
      var deadline = DateTime.UtcNow + timeout;
      while (DateTime.UtcNow < deadline && !observer.IsFinished) {
        Thread.Sleep(50);
      }
    }

    return new Result(stopwatch.Elapsed, observer.UpdateCount, observer.RowCount, observer.PeakRowCount,
      rowsAtOpen, TimedOut: !observer.IsFinished,
      observer.Error == null ? null : Unwrap(observer.Error));
  }

  /// <summary>
  /// The subscription thread surfaces errors wrapped in an AggregateException; the inner one is the
  /// interesting part.
  /// </summary>
  private static Exception Unwrap(Exception e) =>
    e is AggregateException agg && agg.InnerExceptions.Count > 0 ? Unwrap(agg.InnerExceptions[0]) : e;

  public sealed record Result(
      TimeSpan Elapsed,
      int UpdateCount,
      long RowCount,
      long PeakRowCount,
      long RowsAtOpen,
      bool TimedOut,
      Exception? Error) {

    /// <summary>
    /// True when the table never arrived - either the subscription raised, or it ran out of time before
    /// reaching the row count the table reported when we opened it.
    /// </summary>
    public bool Failed => Error != null || PeakRowCount < RowsAtOpen;

    public void Report() {
      if (Error != null) {
        Console.WriteLine($"  FAILED after {UpdateCount} update(s), {Elapsed.TotalSeconds:N1}s");
        Console.WriteLine($"  {Error.GetType().Name}: {Error.Message}");
        return;
      }

      // A table whose rows are being removed can legitimately end below its opening count, so the peak is
      // what says whether the initial snapshot was ever fully delivered.
      if (PeakRowCount >= RowsAtOpen) {
        Console.WriteLine($"  filled: reached {PeakRowCount:N0} rows over {UpdateCount} update(s) " +
                          $"in {Elapsed.TotalMilliseconds:N0}ms");
        return;
      }

      Console.WriteLine($"  INCOMPLETE{(TimedOut ? " (timed out)" : "")}: reached only {PeakRowCount:N0} of " +
                        $"{RowsAtOpen:N0} rows over {UpdateCount} update(s) in {Elapsed.TotalMilliseconds:N0}ms");
    }
  }

  /// <summary>
  /// Logs each update and decides when the subscription has settled.
  /// </summary>
  private sealed class Observer(long rowsAtOpen, bool verbose) : IObserver<TickingUpdate> {
    /// <summary>
    /// How many updates to log before going quiet. The interesting ones are the snapshot rounds at the
    /// start; a ticking table then produces small updates indefinitely.
    /// </summary>
    private const int MaxVerboseUpdates = 25;

    private readonly Stopwatch _sinceSubscribed = Stopwatch.StartNew();
    private double _previousAtMs;

    public int UpdateCount;
    public long RowCount;

    /// <summary>
    /// The most rows we ever held. Tracked separately because a table whose rows are being removed can drop
    /// back below its opening count, which must not read as an incomplete fill.
    /// </summary>
    public long PeakRowCount;

    public Exception? Error;
    public volatile bool IsFinished;

    public void OnNext(TickingUpdate update) {
      var nowMs = _sinceSubscribed.Elapsed.TotalMilliseconds;
      ++UpdateCount;
      RowCount = update.Current.NumRows;
      PeakRowCount = Math.Max(PeakRowCount, RowCount);

      if (verbose && UpdateCount <= MaxVerboseUpdates) {
        Console.WriteLine(
          $"    #{UpdateCount,-4} +{nowMs - _previousAtMs,7:N0}ms  t={nowMs,8:N0}ms" +
          $"  added={update.AddedRowsIndexSpace.Count,8:N0}" +
          $"  removed={update.RemovedRowsIndexSpace.Count,8:N0}  rows={RowCount,9:N0}");
        if (UpdateCount == MaxVerboseUpdates) {
          Console.WriteLine("    ... (further updates not shown)");
        }
      }
      _previousAtMs = nowMs;

      // Done once we have held at least as many rows as the table reported at open: that is the initial
      // snapshot delivered. A ticking table keeps changing past that point, which is not what this measures.
      if (PeakRowCount >= rowsAtOpen) {
        IsFinished = true;
      }
    }

    public void OnError(Exception error) {
      Error = error;
      IsFinished = true;
    }

    public void OnCompleted() {
      IsFinished = true;
    }
  }
}
