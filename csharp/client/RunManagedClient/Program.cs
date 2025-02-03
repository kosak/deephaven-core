using Apache.Arrow;
using Deephaven.DheClient.Session;
using Deephaven.ManagedClient;

namespace Deephaven.RunManangedClient;

public static class Program {
  public static void Main(string[] args) {
    const string descriptiveName = "mysession";
    const string jsonUrl = "https://kosak-grizzle-xp.int.illumon.com:8123/iris/connection.json";

    var sessionManager = SessionManager.FromUrl(descriptiveName, jsonUrl, false);
  }

  public static void CommunityMain(string[] args) {
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
        "Chars = ii == 5 ? null : (char)('a' + ii)",
        "Bytes = ii == 5 ? null : (byte)(ii)",
        "Shorts = ii == 5 ? null : (short)(ii)",
        "Ints = ii == 5 ? null : (int)(ii)",
        "Longs = ii == 5 ? null : (long)(ii)",
        "Floats = ii == 5 ? null : (float)((float)(ii) + 111.111)",
        "Doubles = ii == 5 ? null : (double)((double)(ii) + 222.222)",
        "Bools = ii == 5 ? null : ((ii % 2) == 0)",
        "Strings = ii == 5 ? null : `hello ` + i",
        "DateTimes = ii == 5 ? null : '2001-03-01T12:34:56Z' + ii * 1000000000",
        "LocalDates = ii == 5 ? null : '2001-03-01' + ((int)ii * 'P1D')",
        "LocalTimes = ii == 5 ? null : '12:34:46'.plus((int)ii * 'PT1S')"
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

  private class MyPrintingVisitor :
     IArrowArrayVisitor<UInt16Array>, // char
     IArrowArrayVisitor<Int8Array>, // Java byte
     IArrowArrayVisitor<Int16Array>,
     IArrowArrayVisitor<Int32Array>,
     IArrowArrayVisitor<Int64Array>,
     IArrowArrayVisitor<FloatArray>,
     IArrowArrayVisitor<DoubleArray>,
     IArrowArrayVisitor<BooleanArray>,
     IArrowArrayVisitor<StringArray>,
     IArrowArrayVisitor<TimestampArray>,
     IArrowArrayVisitor<Date64Array>,
     IArrowArrayVisitor<Time64Array> {

    public void Visit(IArrowArray array) {
      throw new NotImplementedException($"I don't have a handler for {array.GetType().Name}");
    }

    public void Visit(UInt16Array array) => DumpData(array, elt => (char)elt);
    public void Visit(Int8Array array) => DumpData(array, elt => (byte)elt);
    public void Visit(Int16Array array) => DumpData(array);
    public void Visit(Int32Array array) => DumpData(array);
    public void Visit(Int64Array array) => DumpData(array);
    public void Visit(FloatArray array) => DumpData(array);
    public void Visit(DoubleArray array) => DumpData(array);
    public void Visit(BooleanArray array) => DumpData(array);
    public void Visit(StringArray array) => DumpRefData<string>(array);
    public void Visit(TimestampArray array) => DumpData<DateTimeOffset, string>(array, elt => elt.ToString("o"));
    public void Visit(Date64Array array) => DumpData<DateOnly, string>(array, elt => elt.ToString("o"));
    public void Visit(Time64Array array) => DumpData<TimeOnly, string>(array, elt => elt.ToString("o"));

    private void DumpData<T>(IReadOnlyList<T?> values) where T : struct {
      DumpData(values, elt => elt);
    }

    private void DumpData<T, TDest>(IReadOnlyList<T?> values, Func<T, TDest> converter) where T : struct {
      TimeOnly temp;
      foreach (var value in values) {
        if (!value.HasValue) {
          Console.WriteLine("?NULL?");
        } else {
          Console.WriteLine(converter(value.Value));
        }
      }
    }

    private void DumpRefData<T>(IReadOnlyList<T> values) {
      foreach (var value in values) {
        if (value == null) {
          Console.WriteLine("?NULL?");
        } else {
          Console.WriteLine(value);
        }
      }
    }

  }
}
