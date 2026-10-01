//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using Deephaven.Dh_NetClient;

namespace Deephaven.BarrageRepro;

/// <summary>
/// Repro 3: the same root cause as repro 2, reached down a different path and producing a different
/// exception - worth having separately because the error message points somewhere misleading.
///
/// Observed:
///
///   Chunks have inconsistent sizes: [0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4096]
///
/// thrown from AwaitingAdds.ProcessNextChunk. One column has 4096 rows of data and the other 31 have none.
///
/// This reads like a server framing bug, and it is not. The table here is wide (31 columns) and every row is
/// modified on each tick, via a natural_join against a one-row ticking table. The client enters the add
/// phase believing there are rows to add - because it sized that phase from added_rows - and is then handed
/// the record batches for the *modify* phase, where per-column data legitimately differs in length: a
/// message carries modified data only for the columns that actually changed, which is exactly why
/// BarrageUpdateMetadata has a per-column mod_column_nodes vector.
///
/// So the assertion is firing against perfectly well-formed data. The add phase should not have been
/// consuming those chunks at all, and would not have been had it sized itself from added_rows_included (see
/// Repro2_AddedRowsIncluded for the detail on that field).
///
/// Two exceptions, one fix. It is worth fixing with that in mind: making this particular message go away by
/// relaxing the size check would leave the add phase still mis-sized, and the next symptom would be silently
/// wrong data rather than an exception.
/// </summary>
internal static class Repro3_InconsistentChunkSizes {
  public static void Run(Client client) {
    Console.WriteLine();
    Console.WriteLine("=== Repro 3: wide table, all rows modified -> \"Chunks have inconsistent sizes\" ===");
    Console.WriteLine();
    Console.WriteLine("  300k rows x 31 columns, every row modified on each tick. The add phase is");
    Console.WriteLine("  mis-sized and ends up consuming the modify phase's per-column chunks.");
    Console.WriteLine();

    const string script = """
      from deephaven import empty_table, time_table

      # A one-row table that ticks, used to touch every row of the big table on every cycle.
      _repro3_beat = time_table("PT0.002S").update(["Beat = ii"]).last_by()

      # Wide: 30 data columns plus the key. The server's per-round budget is a cell count, so more
      # columns means fewer rows per round, which means more rounds for the snapshot.
      _repro3_cols = ["C%d = ii * %d" % (i, i + 1) for i in range(30)]

      repro3 = (empty_table(300_000)
          .update(["K = ii"] + _repro3_cols)
          .update_view(["JoinKey = (long)0"])
          .natural_join(_repro3_beat, on=[], joins=["Beat"])
          .drop_columns("JoinKey"))
      """;

    var result = Harness.Subscribe(client, "repro3", script, TimeSpan.FromSeconds(60));
    result.Report();

    Console.WriteLine();
    if (result.Error != null) {
      Console.WriteLine("  Reproduced. The listed sizes are a valid modify-phase message: data for the");
      Console.WriteLine("  columns that changed, nothing for the rest. The add phase should not see them.");
    } else {
      Console.WriteLine("  Did not reproduce this run - it is a race against the tick rate.");
      Console.WriteLine("  Try again, or widen the table / raise the tick rate in the script above.");
    }
  }
}
