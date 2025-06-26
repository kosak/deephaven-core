using Deephaven.Dh_NetClient;
using Xunit.Abstractions;

namespace Deephaven.Dh_NetClientTests;

public class UngroupTest(ITestOutputHelper output) {
  [Fact]
  public void UngroupColumns666() {
    output.WriteLine("WAKE THE HELL UP");
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    table = table.Where("ImportDate == `2017-11-01`");

    var byTable = table.Where("Ticker == `AAPL`").View("Ticker", "Close").By("Ticker");
    output.WriteLine(byTable.ToString(true, true));
    var ungrouped = byTable.Ungroup("Close");
    output.WriteLine(ungrouped.ToString(true, true));

    {
      var expected = new TableMaker();
      expected.AddColumn("Ticker", "AAPL");
      expected.AddColumn("Close", [23.5, 24.2, 26.7]);


      output.WriteLine("THIS IS EXPECTED");
      output.WriteLine(expected.ToString(true, true));

      output.WriteLine("THIS IS BYTABLE");
      output.WriteLine(byTable.ToString(true, true));

      TableComparer.AssertSame(expected, byTable);
    }

    {
      var expected = new TableMaker();
      expected.AddColumn("Ticker", ["AAPL", "AAPL", "AAPL"]);
      expected.AddColumn("Close", [23.5, 24.2, 26.7]);
      TableComparer.AssertSame(expected, ungrouped);
    }
  }
}
