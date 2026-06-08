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
    tm.AddColumn<int?[]?>("Int32Col", [[0, 1, 2], [3, null], null, [5]]);
    tm.AddColumn<double?[]?>("DoubleCol", [[0.0, 1.1, 2.2], [3.3, null], null, [5.5]]);

    var at = tm.ToArrowTable();

    var ct = ArrowUtil.ToClientTable(at);
    var col0 = ct.GetColumn(1);
    var chunk = Chunk<IList>.Create((int)ct.NumRows);
    var stupid = RowSequence.CreateSequential(Interval.OfStartAndSize(0, (UInt64)ct.NumRows));
    col0.FillChunk(stupid, chunk, null);
    var z0 = (IList)chunk.Data[0];
    var z1 = (IList<double>)chunk.Data[0];
    var z2 = (IList<double?>)chunk.Data[0];

    var what = z0.GetType();

    var at2 = ct.ToArrowTable();
    TableComparer.AssertSame(at, at2);
  }
}
