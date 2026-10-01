//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics;
using Deephaven.Dh_NetClient;

namespace Deephaven.BarrageRepro;

/// <summary>
/// Repro 1: subscribing to a large table costs time quadratic in the number of rows.
///
/// A plain static table - no ticking, no removes, nothing exotic. The server delivers it as a series of
/// record batches, and the client's cost per batch is proportional to the size of the whole table so far
/// rather than to the size of the batch. Doubling the rows therefore roughly quadruples the time.
///
/// Measured against a local server, 6 columns:
///
///     rows        fill time     us/row
///     250,000         1,011ms      4.0
///     500,000         3,175ms      6.3
///   1,000,000        11,902ms     11.9
///   2,000,000        43,441ms     21.7
///
/// us/row should be flat. That it doubles with every doubling of the row count is the signature of an O(n^2)
/// fill. At 2M rows the client spends 43 seconds assembling a table the server produced in well under one.
///
/// Where the time goes - both of these are per-batch costs that scale with total table size, not batch size:
///
/// 1. TableState.AddData allocates a new column of the new total length for every column on every batch
///    (ArrayColumnSource.CreateOfSameType(newNumRows)) and copies the entire table into it. The Nth batch
///    copies all N-1 batches that came before it.
///
/// 2. TableState.Snapshot clones every column in full, and processing one Barrage message calls it three or
///    four times (prev, afterRemoves, afterAdds, afterModifies in TickingUpdate). So each batch also pays
///    several more full-table copies.
///
/// Both have the same fix: adds to a filling table are appends, and an append does not need to rebuild
/// anything. Grow the column geometrically and write only the new rows; let Snapshot share the live storage
/// and copy only when a write would actually disturb a snapshot someone still holds.
/// </summary>
internal static class Repro1_QuadraticFill {
  public static void Run(Client client) {
    Console.WriteLine();
    Console.WriteLine("=== Repro 1: fill time is quadratic in row count ===");
    Console.WriteLine();
    Console.WriteLine("  A static table, subscribed at four sizes. Watch us/row, which should be flat.");
    Console.WriteLine();

    var sizes = new[] { 250_000, 500_000, 1_000_000, 2_000_000 };
    var results = new List<(int Rows, double Ms)>();

    foreach (var rows in sizes) {
      var name = $"repro1_{rows}";
      var script = $"""
        from deephaven import empty_table
        {name} = empty_table({rows}).update([
            "Sym = `s` + (ii % 1000)",
            "Price = ii * 1.5",
            "Size = (int)(ii % 5000)",
            "Venue = `v` + (ii % 10)",
            "Seq = ii",
            "Ratio = ii / 3.0"])
        """;

      // Not verbose: at these sizes the per-update log drowns out the timing, which is the point here.
      var result = Harness.Subscribe(client, name, script, TimeSpan.FromMinutes(5), verbose: false);
      result.Report();

      if (result.Failed) {
        Console.WriteLine("  (gave up on this size; the remaining sizes would only be slower)");
        break;
      }
      results.Add((rows, result.Elapsed.TotalMilliseconds));
    }

    Console.WriteLine();
    Console.WriteLine($"  {"rows",12}  {"fill",10}  {"us/row",8}  {"vs 2x rows",10}");
    double? previousMs = null;
    foreach (var (rows, ms) in results) {
      var growth = previousMs is null ? "-" : $"{ms / previousMs.Value:N1}x";
      Console.WriteLine($"  {rows,12:N0}  {ms,8:N0}ms  {ms * 1000 / rows,8:N1}  {growth,10}");
      previousMs = ms;
    }

    Console.WriteLine();
    Console.WriteLine("  Linear would hold us/row constant and show ~2.0x per doubling.");
    Console.WriteLine("  Quadratic shows ~4.0x per doubling, with us/row doubling alongside it.");
  }
}
