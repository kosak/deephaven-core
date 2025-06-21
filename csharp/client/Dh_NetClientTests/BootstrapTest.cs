using Deephaven.ManagedClient;
using Xunit.Abstractions;

namespace Deephaven.Dh_NetClientTests;

public class BootstrapTest(ITestOutputHelper testOutputHelper) {

  [Fact]
  public void TestLala() {
    var client = Client.Connect("10.0.4.109:10000");
    var manager = client.Manager;

    var tm = new TableMaker();
    tm.AddColumn("ints", [3, 4, 5, 6]);
    tm.AddColumn<double?>("doubles", [3.3, 4.4, null, 5.5]);
    tm.AddColumn("strings", ["hello", "there", "Deephaven", null]);

    var th = tm.MakeTable(manager);
    var temp = th.ToString(true);
    testOutputHelper.WriteLine(temp);

    th.BindToVariable("kosak1");
  }
}
