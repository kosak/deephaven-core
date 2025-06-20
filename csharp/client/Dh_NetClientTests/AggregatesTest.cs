#if false
using Deephaven.DhClientTests;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Backplane.Grpc;
using Xunit.Abstractions;

namespace Deephaven.Dh_NetClientTests;

public class AggregatesTest {
  private readonly ITestOutputHelper _output;

  public AggregatesTest(ITestOutputHelper output) {
    _output = output;
  }

  [Fact]
  public void TestVariousAggregates() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    table = table.Where("ImportDate == `2017-11-01`");
    var zngaTable = table.Where("Ticker == `ZNGA`");

    var aggTable = zngaTable.View("Close")
      .By(new AggregateCombo(new[] {
        ComboAggregateRequest.Types.Aggregate.Avg(new []{"AvgClose=Close"}),
        ComboAggregateRequest.Types.Aggregate.Sum(new []{"SumClose=Close"}),
        ComboAggregateRequest.Types.Aggregate.Min(new [] {"MinClose=Close"}),
        ComboAggregateRequest.Types.Aggregate.Max(new []{"MaxClose=Close"}),
        ComboAggregateRequest.Types.Aggregate.Count("Count")
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
#endif
