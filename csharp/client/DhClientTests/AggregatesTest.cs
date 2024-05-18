using Deephaven.DeephavenClient.Interop;
using Deephaven.DeephavenClient;
using Xunit.Abstractions;
using System.Xml.Linq;

namespace Deephaven.DhClientTests;

public class AggregatesTest {
  private readonly ITestOutputHelper _output;

  public AggregatesTest(ITestOutputHelper output) {
    _output = output;
    PlatformUtf16.Init();
  }

  [Fact]
  public void TestVariousAggregates() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    table = table.Where("ImportDate == `2017-11-01`");
    var zngaTable = table.Where("Ticker == `ZNGA`");

    var aggTable = zngaTable.View("Close")
      .By(AggregateCombo.Create(new[] {
        Aggregate.Avg("AvgClose=Close"),
        Aggregate.Sum("SumClose=Close"),
        Aggregate.Min("MinClose=Close"),
        Aggregate.Max("MaxClose=Close"),
        Aggregate.Count("Count")
      }));

    var tickerData = new[]{ "AAPL", "AAPL", "AAPL"};
    var avgCloseData = new[] { 541.55 };
    var sumCloseData = new[] { 1083.1 };
    var minCloseData = new[] { 538.2 };
    var maxCloseData = new[] { 544.9 };
    var countData = new Int64[] { 2 };

    var tc = new TableComparer();
    tc.AddColumn("AvgClose", avgCloseData);
    tc.AddColumn("SumClose", sumCloseData);
    tc.AddColumn("MinClose", minCloseData);
    tc.AddColumn("MaxClose", maxCloseData);
    tc.AddColumn("Count", countData);
    tc.AssertEqualTo(aggTable);
  }
}
