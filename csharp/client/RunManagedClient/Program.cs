using Deephaven.ManagedClient;

namespace Deephaven.RunManangedClient;

public static class Program {
  public static void Main(string[] args) {
    var server = "10.0.4.109:10000";
    if (args.Length > 0) {
      if (args.Length != 1 || args[0] == "-h") {
        Console.Error.WriteLine("Arguments: [host:port]");
        Environment.Exit(1);
      }

      server = args[0];
    }

    try {
      using var client = Client.Connect(server);
      using var manager = client.GetManager();
      using var table = manager.EmptyTable(10);
      using var t2 = table.Update("ABC = ii == 5 ? null : ii + 100");
      // using var t2 = table.Update("ABC = ii + 100", "XYZ = '12:34:56.000'");
      Console.WriteLine(t2.ToString(true));
      var at = t2.ToArrowTable();
      var ct = t2.ToClientTable();
      var cs = ct.GetColumn(0);

      var size = ct.NumRows.ToIntExact();
      var chunk = ChunkMaker.CreateChunkFor(cs, size);
      var nulls = BooleanChunk.Create(size);
      var rs = ct.RowSequence;

      cs.FillChunk(rs, chunk, nulls);
      Console.WriteLine("hello");
    } catch (Exception e) {
      Console.Error.WriteLine($"Caught exception: {e}");
    }
  }
}
