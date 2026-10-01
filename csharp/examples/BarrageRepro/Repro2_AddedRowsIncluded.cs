//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using Deephaven.Dh_NetClient;

namespace Deephaven.BarrageRepro;

/// <summary>
/// Repro 2: a large table that is also ticking throws "There is excess data in the chunk that I won't be
/// able to process", and the subscription dies before delivering a single row.
///
/// Observed failures, from three runs against a local server:
///
///   There is excess data in the chunk that I won't be able to process. Expected 1207, have 1212
///   There is excess data in the chunk that I won't be able to process. Expected 3937, have 3963
///   There is excess data in the chunk that I won't be able to process. Expected 138, have 147
///
/// thrown from AwaitingAdds.ProcessNextChunk. The numbers vary run to run - it is a race against the table's
/// tick rate - but the shape is always the same: the record batches carry a few more rows than the metadata
/// led the client to expect.
///
/// The cause is that the client sizes the add phase from the wrong field of BarrageUpdateMetadata.
/// AwaitingMetadata reads added_rows and uses it for both "what the table gains" and "what this message
/// carries data for". Those are different sets, and the metadata has a separate field for the second one:
///
///   added_rows           - the rows the table gains
///   added_rows_included  - the rows these record batches actually contain data for
///
/// The server omits added_rows_included when it would be identical to added_rows, so absent means "same as
/// added_rows" - which is why small tables work and this only shows up once the table is big enough for the
/// server to split the snapshot into rounds. BarrageMessageWriterImpl.getSubscriptionMetadata:
///
///   // don't send `rowsIncluded` to viewport clients or if identical to `rowsAdded`
///   if (isFullSubscription &amp;&amp; (isSnapshot || !clientIncludedRows.equals(rowsAdded.original))) {
///       addedRowsIncludedOffset = rowsIncluded.addToFlatBuffer(clientIncludedRows, metadata);
///   }
///
/// The Java client reads it; BarrageMessageReaderImpl:
///
///   msg.rowsIncluded = rowsIncluded != null ? extractIndex(rowsIncluded) : msg.rowsAdded.copy();
///   numAddRowsTotal = msg.rowsIncluded.size();
///
/// The .NET client has no equivalent - BarrageUpdateMetadata.GetAddedRowsIncludedBytes() is generated and
/// available, but never called. The add phase is sized from added_rows, the data is sized from
/// added_rows_included, and when a growing snapshot makes them differ the chunk arithmetic breaks.
///
/// Why they differ here: while the snapshot is still being delivered the server interleaves deltas with the
/// snapshot rounds, so one message can both announce newly-ticked rows and carry data for rows from the
/// snapshot - "While the subscription viewport is growing, it may receive deltas on the rows that have
/// already been snapshotted and sent to the client" (BarrageMessageProducer). Note also that during a
/// growing snapshot the server deliberately sends an empty added_rows:
///
///   } else if (isSnapshot &amp;&amp; !isInitialSnapshot) {
///       // Growing viewport clients don't need/want to receive the full RowSet on every snapshot
///       rowsAddedOffset = EmptyRowSetWriter.INSTANCE.addToFlatBuffer(metadata);
///
/// so added_rows is not merely a different set, it can be empty while a full batch of data arrives.
///
/// Reading added_rows_included when present - and positioning keys from it rather than from added_rows - is
/// what the add phase needs.
/// </summary>
internal static class Repro2_AddedRowsIncluded {
  public static void Run(Client client) {
    Console.WriteLine();
    Console.WriteLine("=== Repro 2: large + ticking table -> \"excess data in the chunk\" ===");
    Console.WriteLine();
    Console.WriteLine("  500k rows already present, plus a row every 2ms, so the server interleaves");
    Console.WriteLine("  deltas with the snapshot rounds and added_rows stops matching the data sent.");
    Console.WriteLine();

    const string script = """
      from deephaven import empty_table, time_table, merge

      # Large enough that the server splits the snapshot into several rounds...
      _repro2_base = empty_table(500_000).update([
          "K = ii",
          "Price = ii * 1.5",
          "Sym = `s` + (ii % 100)"])

      # ...and ticking, so deltas arrive while those rounds are still being delivered.
      _repro2_tick = time_table("PT0.002S").update([
          "K = 9_000_000 + ii",
          "Price = (double)ii",
          "Sym = `t`"]).drop_columns("Timestamp")

      repro2 = merge([_repro2_base, _repro2_tick])
      """;

    var result = Harness.Subscribe(client, "repro2", script, TimeSpan.FromSeconds(60));
    result.Report();

    Console.WriteLine();
    if (result.Error != null) {
      Console.WriteLine("  Reproduced. The subscription raised before delivering the table.");
      Console.WriteLine("  Expected N / have M: M is the data the batches carry (added_rows_included),");
      Console.WriteLine("  N is what the client budgeted from added_rows.");
    } else {
      Console.WriteLine("  Did not reproduce this run - it is a race against the tick rate.");
      Console.WriteLine("  Try again, or raise the row count / tick rate in the script above.");
    }
  }
}
