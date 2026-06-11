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
    tm.AddColumn<sbyte?[]?>("Int8", [[0, 1, 2], [3, 4, null], null, [6]]);
    tm.AddColumn<Int16?[]?>("Int16", [[0, 1, 2], [3, 4, null], null, [6]]);
    tm.AddColumn<Int32?[]?>("Int32", [[0, 1, 2], [3, 4, null], null, [6]]);
    tm.AddColumn<Int64?[]?>("Int64", [[0, 1, 2], [3, 4, null], null, [6]]);
    tm.AddColumn<float?[]?>("float", [[0.0f, 1.1f, 2.2f], [3.3f, 4.4f, null], null, [6.6f]]);
    tm.AddColumn<double?[]?>("double", [[0.0, 1.1, 2.2], [3.3, 4.4, null], null, [6.6]]);
    tm.AddColumn<bool?[]?>("bool", [[false, true], [false, true, null], null, [true]]);
    tm.AddColumn<string?[]?>("string", [["", "hello"], ["a", "b", null], null, ["c"]]);

    var at = tm.ToArrowTable();

    var ct = ArrowUtil.ToClientTable(at);
    var col0 = ct.GetColumn(1);
    var chunk = Chunk<IList>.Create((int)ct.NumRows);
    var stupid = RowSequence.CreateSequential(Interval.OfStartAndSize(0, (UInt64)ct.NumRows));
    col0.FillChunk(stupid, chunk, null);
    // var z0 = (IList)chunk.Data[0];
    // var z1 = (IList<double>)chunk.Data[0];
    // var z2 = (IList<double?>)chunk.Data[0];
    //
    // var what = z0.GetType();

    var at2 = ct.ToArrowTable();
    TableComparer.AssertSame(at, at2);
    Debug.WriteLine("BYE");
  }
}
