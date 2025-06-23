using Deephaven.Dh_NetClient;
using Io.Deephaven.Proto.Backplane.Grpc;
using Xunit.Abstractions;

namespace Deephaven.Dh_NetClientTests;

public class AggregatesTest(ITestOutputHelper output) {
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

    var expected = new TableMaker();
    expected.AddColumn("AvgClose", [541.55]);
    expected.AddColumn("SumClose", [1083.1]);
    expected.AddColumn("MinClose", [538.2]);
    expected.AddColumn("MaxClose", [544.9]);
    expected.AddColumn("Count", [(Int64)2]);

    TableComparer.AssertSame(expected, aggTable);
  }
}
