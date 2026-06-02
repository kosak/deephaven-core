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
    tm.AddColumn<int[]>("Col1", [[0, 1, 2, 9, 9, 9], [3, 4], [5]]);

    var at = tm.ToArrowTable();

    // TODO(kosak): When you fix this, update DH-19910
    var ct = ArrowUtil.ToClientTable(at);
    var at2 = ct.ToArrowTable();
    TableComparer.AssertSame(at, at2);
  }
}
