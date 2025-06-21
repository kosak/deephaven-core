using Deephaven.ManagedClient;
using Xunit.Abstractions;

namespace Deephaven.Dh_NetClientTests;

public class BootstrapTest(ITestOutputHelper testOutputHelper) {

  [Fact]
  public void TestLala() {
    var client = Client.Connect("10.0.4.109:10000");
    var manager = client.Manager;

    var tm = new TableMaker();
    tm.AddColumn("KOSAK1", [3, 4, 5, 6]);
    tm.AddColumn<int?>("KOSAK2", [3, 4, null, 6]);
    tm.AddColumn("kosak3", ["hello", "there", "Deephaven", null]);

    var th = tm.MakeTable(manager);
    var temp = th.ToString(true);
    testOutputHelper.WriteLine(temp);

  }
}
