using Apache.Arrow;
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
      using var manager = client.Manager;
      using var t1 = manager.EmptyTable(10);
      using var t2 = t1.Update(
        // "Chars = ii == 5 ? null : (char)('a' + ii)",
        // "Bytes = ii == 5 ? null : (byte)(ii)",
        "Shorts = ii == 5 ? null : (short)(ii)",
        "Ints = ii == 5 ? null : (int)(ii)",
        "Longs = ii == 5 ? null : (long)(ii)",
        "Floats = ii == 5 ? null : (float)(ii)",
        "Doubles = ii == 5 ? null : (double)(ii)"
        // "Bools = ii == 5 ? null : ((ii % 2) == 0)",
        // "Strings = ii == 5 ? null : `hello ` + i",
        // "DateTimes = ii == 5 ? null : '2001-03-01T12:34:56Z' + ii",
        // "LocalDates = ii == 5 ? null : parseLocalDate(`2001-3-` + (ii + 1))",
        // "LocalTimes = ii == 5 ? null : parseLocalTime(`12:34:` + (46 + ii))"
      );

      var at = t2.ToArrowTable();

      Console.WriteLine("printing table manually");

      var myPrintingVisitor = new MyPrintingVisitor();

      var ncols = at.ColumnCount;
      for (var colIndex = 0; colIndex != ncols; ++colIndex) {
        Console.WriteLine($"=== column {colIndex} ===");
        var col = at.Column(colIndex);

        var chunkedArray = col.Data;

        for (var arrayIndex = 0; arrayIndex != chunkedArray.ArrayCount; ++arrayIndex) {
          var array = chunkedArray.ArrowArray(arrayIndex);

          array.Accept(myPrintingVisitor);
        }
      }
    } catch (Exception e) {
      Console.Error.WriteLine($"Caught exception: {e}");
    }
  }

  private class MyPrintingVisitor : IArrowArrayVisitor,
     IArrowArrayVisitor<Int16Array>,
     IArrowArrayVisitor<Int32Array>,
     IArrowArrayVisitor<Int64Array>,
     IArrowArrayVisitor<FloatArray>,
     IArrowArrayVisitor<DoubleArray> {

    public void Visit(IArrowArray array) {
      throw new NotImplementedException($"I don't have a handler for {array.GetType().Name}");
    }

    public void Visit(Int16Array array) => DumpData(array, array.Values);
    public void Visit(Int32Array array) => DumpData(array, array.Values);
    public void Visit(Int64Array array) => DumpData(array, array.Values);
    public void Visit(FloatArray array) => DumpData(array, array.Values);
    public void Visit(DoubleArray array) => DumpData(array, array.Values);

    private void DumpData<T>(IArrowArray a, ReadOnlySpan<T> values) {
      for (var i = 0; i != a.Length; ++i) {
        if (a.IsNull(i)) {
          Console.WriteLine("?NULL?");
        } else {
          Console.WriteLine(values[i]);
        }
      }
    }
  }
}
