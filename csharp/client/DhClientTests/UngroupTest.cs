using Deephaven.DeephavenClient;
using System;

namespace Deephaven.DhClientTests;

public class UngroupTest {
  [Fact]
  public void UngroupColumns() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());

    using var table = ctx.TestTable.Where("ImportDate == `2017-11-01`");

    var byTable = table.Where("Ticker == `AAPL`").View("Ticker", "Close").View("Ticker");
    var ungrouped = byTable.Ungroup("Close");

    {
      var tickerData = new[] { "AAPL" };
      var closeData = new[] { "[23.5,24.2,26.7]" };
      var tc = new TableComparer();
      tc.AddColumn("Ticker", tickerData);
      tc.AddColumn("Close", closeData);
      tc.AssertEqualTo(byTable);
    }

    {
      var ugTickerData = new[] { "AAPL", "AAPL", "AAPL" };
      var ugCloseData = new[] { 23.5, 24.2, 26.7 };
      var tc = new TableComparer();
      tc.AddColumn("Ticker", ugTickerData);
      tc.AddColumn("Close", ugCloseData);
      tc.AssertEqualTo(ungrouped);
    }
  }
}
