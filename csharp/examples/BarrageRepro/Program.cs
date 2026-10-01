//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using Deephaven.BarrageRepro;

// Four independent problems a .NET client hits when it subscribes to a large table. Each is a separate file
// with its own write-up; run them individually by name, or with no argument to run them all.
//
//   dotnet run            -- all four
//   dotnet run 2          -- just repro 2
//
// Set DH_HOST / DH_PORT if the server is not at localhost:10000. The server needs a Python session, since
// each repro defines its table with a small Python snippet.

var repros = new (string Name, string Summary, Action<Deephaven.Dh_NetClient.Client> Run)[] {
  ("1", "fill time is quadratic in row count", Repro1_QuadraticFill.Run),
  ("2", "large + ticking -> \"excess data in the chunk\"", Repro2_AddedRowsIncluded.Run),
  ("3", "wide + modified -> \"Chunks have inconsistent sizes\"", Repro3_InconsistentChunkSizes.Run),
  ("4", "removes for undelivered rows -> SpaceMapper throws", Repro4_RemovesOutsideDeliveredRows.Run),
};

var selected = args.Length == 0
  ? repros
  : repros.Where(r => args.Contains(r.Name)).ToArray();

if (selected.Length == 0) {
  Console.WriteLine("No repro matched. Available:");
  foreach (var (name, summary, _) in repros) {
    Console.WriteLine($"  {name}  {summary}");
  }
  return 1;
}

using var client = Harness.Connect();

foreach (var (name, _, run) in selected) {
  try {
    run(client);
  } catch (Exception e) {
    // A repro throwing on the calling thread is still a result worth seeing, so keep going.
    Console.WriteLine($"  repro {name} threw on the main thread: {e.GetType().Name}: {e.Message}");
  }
}

Console.WriteLine();
return 0;
