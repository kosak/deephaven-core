using System.Collections;
using System.Diagnostics;
using Deephaven.Dh_NetClient;

namespace ListTthing;

internal class Program {
  public static void Main(string[] args) {
    try {
      Nested();
    } catch (Exception ex) {
      Debug.WriteLine($"hi: {ex}");
    }
  }

  private static void Nested() {
    var tm = new TableMaker();
    tm.AddColumn<Int32?[]?>("test-Int32", [[0, 1, 2], [3, 4, null], null, [6]]);

    tm.AddColumn<sbyte?[]?>("Int8", [[0, 1, 2], [3, 4, null], null, [6]]);
    tm.AddColumn<Int16?[]?>("Int16", [[0, 1, 2], [3, 4, null], null, [6]]);
    tm.AddColumn<Int32?[]?>("Int32", [[0, 1, 2], [3, 4, null], null, [6]]);
    tm.AddColumn<Int64?[]?>("Int64", [[0, 1, 2], [3, 4, null], null, [6]]);
    tm.AddColumn<float?[]?>("float", [[0.0f, 1.1f, 2.2f], [3.3f, 4.4f, null], null, [6.6f]]);
    tm.AddColumn<double?[]?>("double", [[0.0, 1.1, 2.2], [3.3, 4.4, null], null, [6.6]]);
    tm.AddColumn<bool?[]?>("bool", [[false, true], [false, true, null], null, [true]]);
    tm.AddColumn<string?[]?>("string", [["", "hello"], ["a", "b", null], null, ["c"]]);
    tm.AddColumn<char?[]?>("char", [['a', (char)0], ['a', 'b', null], null, ['c']]);
    var dto1 = new DateTimeOffset(1966, 3, 1, 12, 34, 56, TimeSpan.Zero);
    var dto2 = new DateTimeOffset(1999, 12, 31, 3, 44, 55, TimeSpan.Zero);
    tm.AddColumn<DateTimeOffset?[]?>("dateTimeOffset", [[dto1, dto2], [DateTimeOffset.MinValue, DateTimeOffset.MaxValue, null], null, [dto2]]);
    tm.AddColumn<DateOnly?[]?>("dateOnly", [[DateOnly.FromDateTime(dto1.Date), DateOnly.FromDateTime(dto2.Date)], [DateOnly.MinValue, DateOnly.MaxValue, null], null, [DateOnly.FromDateTime(dto2.Date)]]);
    tm.AddColumn<TimeOnly?[]?>("timeOnly", [[TimeOnly.FromDateTime(dto1.Date), TimeOnly.FromDateTime(dto2.Date)], [TimeOnly.MinValue, TimeOnly.MaxValue, null], null, [TimeOnly.FromDateTime(dto2.Date)]]);

    var at = tm.ToArrowTable();

    var ct = ArrowUtil.ToClientTable(at);
    var col0 = ct.GetColumn(0);
    var chunk = Chunk<IList>.Create((int)ct.NumRows);
    var stupid = RowSequence.CreateSequential(Interval.OfStartAndSize(0, (UInt64)ct.NumRows));
    col0.FillChunk(stupid, chunk, null);

    Debug.WriteLine(DumpChunk1(chunk));
    Debug.WriteLine(DumpChunk2<Int32?>(chunk));
    Debug.WriteLine(DumpChunk2<Int32>(chunk));

    var at2 = ct.ToArrowTable();
    TableComparer.AssertSame(at, at2);
    Debug.WriteLine("BYE");
  }

  private static string DumpChunk1(Chunk<IList> chunk) {
    var sw = new StringWriter();
    var sep = "";
    sw.Write('[');
    for (var i = 0; i != chunk.Size; i++) {
      sw.Write(sep);
      sep = ",";

      var element = chunk.Data[i];
      if (element == null) {
        sw.Write("null");
        continue;
      }

      sw.Write('[');
      var innerSep = "";
      for (var j = 0; j != element.Count; j++) {
        sw.Write(innerSep);
        innerSep = ",";
        var item = element[j];
        sw.Write(item == null ? "NULL" : item.ToString());
      }
      sw.Write(']');
    }
    sw.Write(']');
    return sw.ToString();
  }

  private static string DumpChunk2<T>(Chunk<IList> chunk) {

    var sw = new StringWriter();
    var sep = "";
    sw.Write('[');
    for (var i = 0; i != chunk.Size; i++) {
      sw.Write(sep);
      sep = ",";

      var element = (IList<T>?)chunk.Data[i];
      if (element == null) {
        sw.Write("null");
        continue;
      }

      sw.Write('[');
      var innerSep = "";
      for (var j = 0; j != element.Count; j++) {
        sw.Write(innerSep);
        innerSep = ",";
        var item = element[j];
        sw.Write(item == null ? "NULL" : item.ToString());
      }
      sw.Write(']');
    }
    sw.Write(']');
    return sw.ToString();
  }
}

