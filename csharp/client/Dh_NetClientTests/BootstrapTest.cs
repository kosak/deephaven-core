using Deephaven.ManagedClient;
using Xunit.Abstractions;

namespace Deephaven.Dh_NetClientTests;

public class BootstrapTest(ITestOutputHelper testOutputHelper) {

  [Fact]
  public void TestLala() {
    var tm = new TableMaker();
    tm.AddColumn("int", [3, 4, 5, 6]);
    tm.AddColumn<int?>("intopt", [3, 4, null, 6]);
    tm.AddColumn("string", ["hello", "there", null]);
  }
}
