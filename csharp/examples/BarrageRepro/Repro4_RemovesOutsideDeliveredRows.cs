//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using Deephaven.Dh_NetClient;

namespace Deephaven.BarrageRepro;

/// <summary>
/// Repro 4: a remove naming rows the client has not been sent yet throws out of SpaceMapper.
///
/// Observed, from three runs:
///
///   Of the 24 keys specified in Interval { Begin = 299959, End = 299983, ... }, the set only contains 0 keys
///   Of the 25 keys specified in Interval { Begin = 299963, End = 299988, ... }, the set only contains 0 keys
///   Of the 25 keys specified in Interval { Begin = 299952, End = 299977, ... }, the set only contains 0 keys
///
/// thrown from SpaceMapper.ConvertKeysToIndices, reached from TableState.Erase.
///
/// A fair warning about reproducing this one: on a completely unpatched client it is usually masked. Either
/// the add-phase bug (repros 2 and 3) raises first, or the quadratic fill (repro 1) is still grinding through
/// the first rounds when the test gives up. It becomes reliable - 3 out of 3 above - once the
/// added_rows_included fix is in place. So this is best read as the bug waiting behind that one, rather than
/// as something you will see on the first run. If the run below reports success or delivers nothing at all,
/// that is the masking, not the absence of the bug.
///
/// The table is 300k rows with a ticking blacklist that eats the keyspace from the top down, while the
/// growing snapshot is delivering it from the bottom up. So the server sends removes for high keys the client
/// has not received yet. Erase converts those keys strictly and throws when they are not in the map.
///
/// This is normal traffic, not a server fault. A full subscription is sent the server's entire removed set,
/// unscoped. BarrageMessageProducer.enqueueUpdate copies upstream.removed() verbatim, and
/// BarrageMessageWriterImpl writes it without intersecting what the client has been sent:
///
///   if (isFullSubscription) {
///       clientRemovedRows = null; // we'll send full subscriptions the full removed set
///
/// The client is expected to free the rows it holds while still tracking the table's overall row set, so a
/// remove naming keys outside the delivered prefix has to be skipped, not rejected.
///
/// Two further consequences of that, both of which bite in the same place:
///
/// 1. The row count must be decremented by the number of rows actually erased, not by the size of the
///    requested set. TableState.Erase currently uses rowsToEraseKeySpace.Count, which over-counts whenever
///    the removed set reaches past the delivered rows, and leaves _numRows disagreeing with the columns.
///
/// 2. A non-empty removed set can erase nothing at all. BarrageProcessor.ProcessRemoves takes the
///    "unchanged" pointer-equality path only when removedRows.IsEmpty; it should take it whenever nothing
///    was actually erased, so that beforeRemoves == afterRemoves keeps meaning what consumers rely on.
/// </summary>
internal static class Repro4_RemovesOutsideDeliveredRows {
  public static void Run(Client client) {
    Console.WriteLine();
    Console.WriteLine("=== Repro 4: removes naming undelivered rows -> SpaceMapper throws ===");
    Console.WriteLine();
    Console.WriteLine("  300k rows x 31 columns. A ticking blacklist removes rows from the top of the");
    Console.WriteLine("  keyspace while the snapshot is still being delivered from the bottom.");
    Console.WriteLine();
    Console.WriteLine("  NOTE: on a fully unpatched client this is usually masked by repro 1/2/3.");
    Console.WriteLine("  It becomes reliable once added_rows_included is handled. See the file comment.");
    Console.WriteLine();

    const string script = """
      from deephaven import empty_table, time_table

      _repro4_cols = ["C%d = ii * %d" % (i, i + 1) for i in range(30)]
      _repro4_base = empty_table(300_000).update(["K = ii"] + _repro4_cols)

      # Counts DOWN from the highest key, so the rows being removed are the ones the growing
      # snapshot has not reached yet.
      _repro4_blacklist = time_table("PT0.002S").update([
          "K = (long)(299_999 - ii)"]).drop_columns("Timestamp")

      repro4 = _repro4_base.where_not_in(_repro4_blacklist, "K")
      """;

    var result = Harness.Subscribe(client, "repro4", script, TimeSpan.FromSeconds(60));
    result.Report();

    Console.WriteLine();
    if (result.Error is not null && result.Error.Message.Contains("the set only contains")) {
      Console.WriteLine("  Reproduced. Those keys are rows the server removed but had not yet sent us.");
    } else if (result.Error != null) {
      Console.WriteLine("  Raised, but with a different error - one of the earlier bugs fired first.");
      Console.WriteLine("  Fix repro 2/3 and run this again.");
    } else {
      Console.WriteLine("  Did not reproduce this run, most likely masked - see the note above.");
    }
  }
}
