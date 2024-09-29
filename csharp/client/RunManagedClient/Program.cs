global using BooleanChunk = Deephaven.ManagedClient.Chunk<bool>;

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
      using var t1 = manager.EmptyTable(10);
      using var t2 = t1.Update(
        "Chars = ii == 5 ? null : (char)('a' + ii)",
        "Bytes = ii == 5 ? null : (byte)(ii)",
        "Shorts = ii == 5 ? null : (short)(ii)",
        "Ints = ii == 5 ? null : (int)(ii)",
        "Longs = ii == 5 ? null : (long)(ii)",
        "Floats = ii == 5 ? null : (float)(ii)",
        "Doubles = ii == 5 ? null : (double)(ii)",
        "Bools = ii == 5 ? null : ((ii % 2) == 0)",
        "Strings = ii == 5 ? null : `hello ` + i",
        "DateTimes = ii == 5 ? null : '2001-03-01T12:34:56Z' + ii",
        "LocalDates = ii == 5 ? null : parseLocalDate(`2001-3-` + (ii + 1))",
        "LocalTimes = ii == 5 ? null : parseLocalTime(`12:34:` + (46 + ii))"
      );

      var tResult = t2;

      Console.WriteLine(tResult.ToString(true));
      // var at = tResult.ToArrowTable();
      var ct = tResult.ToClientTable();
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
