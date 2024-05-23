using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.Utility;
using System;
using Xunit.Abstractions;
using static Deephaven.DeephavenClient.UpdateByOperation;

namespace Deephaven.DhClientTests;

public class UpdateByTest {
  const int NumCols = 5;
  const int NumRows = 1000;

  private readonly ITestOutputHelper _output;

  public UpdateByTest(ITestOutputHelper output) {
    _output = output;
  }


  [Fact]
  public void SimpleCumSum() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var tm = ctx.Client.Manager;

    var source = tm.EmptyTable(10).Update("Letter = (i % 2 == 0) ? `A` : `B`", "X = i");
    var result = source.UpdateBy(new[] { CumSum(new[] { "SumX = X" }) }, new[] { "Letter" });
    var filtered = result.Select("SumX");
    var tc = new TableComparer();
    tc.AddColumn("SumX", new Int64[] { 0, 1, 2, 4, 6, 9, 12, 16, 20, 25 });
    tc.AssertEqualTo(filtered);
  }

  [Fact]
  public void SimpleOps() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var tm = ctx.Client.Manager;

    var tables = MakeTables(tm);
    var simpleOps = MakeSimpleOps();

    for (var opIndex = 0; opIndex != simpleOps.size(); ++opIndex) {
      var op = simpleOps[opIndex];
      for (var tableIndex = 0; tableIndex != tables.Length; ++tableIndex) {
        var table = tables[tableIndex];
        _output.WriteLine($"Processing op {opIndex} on Table {tableIndex}");
        using var result = table.UpdateBy(new[] {op}, new[] {"e"});
        Assert.Equal(table.IsStatic, result.IsStatic);
        Assert.Equal(2 + table.NumCols, result.NumCols);
        Assert.True(result.NumRows >= table.NumRows);
      }
    }
  }

  [Fact]
  public void EmOps() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var tm = ctx.Client.Manager;

    var tables = MakeTables(tm);
    var emOps = MakeEmOps();

    for (var opIndex = 0; opIndex != emOps.size(); ++opIndex) {
      var op = emOps[opIndex];
      for (var tableIndex = 0; tableIndex != tables.Length; ++tableIndex) {
        var table = tables[tableIndex];
        _output.WriteLine($"Processing op {opIndex} on Table {tableIndex}");
        using var result = table.UpdateBy(new[] { op }, new[] { "b" });
        Assert.Equal(table.IsStatic, result.IsStatic);
        Assert.Equal(1 + table.NumCols, result.NumCols);
        if (result.IsStatic) {
          Assert.Equal(result.NumRows, table.NumRows);
        }
      }
    }
  }

  [Fact]
  public void RollingOps() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var tm = ctx.Client.Manager;

    var tables = MakeTables(tm);
    var rollingOps = MakeRollingOps();

    for (var opIndex = 0; opIndex != rollingOps.Length; ++opIndex) {
      var op = rollingOps[opIndex];
      for (var tableIndex = 0; tableIndex != tables.Length; ++tableIndex) {
        var table = tables[tableIndex];
        _output.WriteLine($"Processing op {opIndex} on Table {tableIndex}");
        using var result = table.UpdateBy(new[] { op }, new[] { "c" });
        Assert.Equal(table.IsStatic, result.IsStatic);
        Assert.Equal(2 + table.NumCols, result.NumCols);
        Assert.True(result.NumRows >= table.NumRows);
      }
    }
  }

  [Fact]
  public void MultipleOps() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var tm = ctx.Client.Manager;

    var tables = MakeTables(tm);
    var multipleOps = {
      CumSum(new[] { "sum_a=a", "sum_b=b" }),
      CumSum(new[] { "max_a=a", "max_d=d" }),
      EmaTick(10, new[] { "ema_d=d", "ema_e=e" }),
      EmaTime("Timestamp", "PT00:00:00.1", new[] { "ema_time_d=d", "ema_time_e=e" }),
      RollingWavgTick("b", new[] { "rwavg_a = a", "rwavg_d = d" }, 10)
    };

    for (var tableIndex = 0; tableIndex != tables.Length; ++tableIndex) {
      var table = tables[tableIndex];
      _output.WriteLine($"Processing table {tableIndex}");
      using var result = table.UpdateBy(new[] { op }, new[] { "c" });
      Assert.Equal(table.IsStatic, result.IsStatic);
      Assert.Equal(10 + table.NumCols, result.NumCols);
      if (result.IsStatic) {
        Assert.Equal(result.NumRows, table.NumRows);
      }
    }
  }

  private TableHandle[] MakeTables(TableHandleManager tm) {
    var staticTable = tm.MakeRandomTable(tm).Update("Timestamp=now()");
    var tickingTable = tm.TimeTable(TimeSpan.FromSeconds(1))
        .Update("a = i", "b = i*i % 13", "c = i * 13 % 23", "d = a + b", "e = a - b");
    return new [] { staticTable, tickingTable};
  }

  TableHandle MakeRandomTable(const Client &client) {
    std::random_device rd;
    std::default_random_engine engine(rd());
    std::uniform_int_distribution<int32_t> uniform_dist(0, 999);

    TableMaker tm;
    static_assert(kNumCols <= 26);
    for (size_t col = 0; col != kNumCols; ++col) {
      char name[2] = { static_cast<char>('a' + col), 0 };
      auto values = MakeReservedVector<int32_t>(kNumRows);
      for (size_t i = 0; i != kNumRows; ++i) {
        values.push_back(uniform_dist(engine));
      }
      tm.AddColumn(name, values);
    }
    return tm.MakeTable(client.GetManager());
  }

  std::vector<UpdateByOperation> MakeSimpleOps() {
    std::vector < std::string> simple_op_pairs = { "UA=a", "UB=b"};
    std::vector<UpdateByOperation> result = {
      cumSum(simple_op_pairs),
      cumProd(simple_op_pairs),
      cumMin(simple_op_pairs),
      cumMax(simple_op_pairs),
      forwardFill(simple_op_pairs),
      delta(simple_op_pairs),
      delta(simple_op_pairs, DeltaControl::kNullDominates),
      delta(simple_op_pairs, DeltaControl::kValueDominates),
      delta(simple_op_pairs, DeltaControl::kZeroDominates)
  };
    return result;
  }

  std::vector<UpdateByOperation> MakeEmOps() {
    OperationControl em_op_control(BadDataBehavior::kThrow, BadDataBehavior::kReset,
        MathContext::kUnlimited);

    using nanos = std::chrono::nanoseconds;

    std::vector<UpdateByOperation> result = {
      // exponential moving average
      emaTick(100, {"ema_a = a"}),
      emaTick(100, { "ema_a = a"}, em_op_control),
      emaTime("Timestamp", nanos(10), { "ema_a = a"}),
      emaTime("Timestamp", "PT00:00:00.001", { "ema_c = c"}, em_op_control),
      emaTime("Timestamp", "PT1M", { "ema_c = c"}),
      emaTime("Timestamp", "PT1M", { "ema_c = c"}, em_op_control),
      // exponential moving sum
      emsTick(100, { "ems_a = a"}),
      emsTick(100, { "ems_a = a"}, em_op_control),
      emsTime("Timestamp", nanos(10), { "ems_a = a"}),
      emsTime("Timestamp", "PT00:00:00.001", { "ems_c = c"}, em_op_control),
      emsTime("Timestamp", "PT1M", { "ema_c = c"}),
      emsTime("Timestamp", "PT1M", { "ema_c = c"}, em_op_control),
      // exponential moving minimum
      emminTick(100, { "emmin_a = a"}),
      emminTick(100, { "emmin_a = a"}, em_op_control),
      emminTime("Timestamp", nanos(10), { "emmin_a = a"}),
      emminTime("Timestamp", "PT00:00:00.001", { "emmin_c = c"}, em_op_control),
      emminTime("Timestamp", "PT1M", { "ema_c = c"}),
      emminTime("Timestamp", "PT1M", { "ema_c = c"}, em_op_control),
      // exponential moving maximum
      emmaxTick(100, { "emmax_a = a"}),
      emmaxTick(100, { "emmax_a = a"}, em_op_control),
      emmaxTime("Timestamp", nanos(10), { "emmax_a = a"}),
      emmaxTime("Timestamp", "PT00:00:00.001", { "emmax_c = c"}, em_op_control),
      emmaxTime("Timestamp", "PT1M", { "ema_c = c"}),
      emmaxTime("Timestamp", "PT1M", { "ema_c = c"}, em_op_control),
      // exponential moving standard deviation
      emstdTick(100, { "emstd_a = a"}),
      emstdTick(100, { "emstd_a = a"}, em_op_control),
      emstdTime("Timestamp", nanos(10), { "emstd_a = a"}),
      emstdTime("Timestamp", "PT00:00:00.001", { "emtd_c = c"}, em_op_control),
      emstdTime("Timestamp", "PT1M", { "ema_c = c"}),
      emstdTime("Timestamp", "PT1M", { "ema_c = c"}, em_op_control)
  };
  return result;
}

std::vector<UpdateByOperation> MakeRollingOps() {
  using secs = std::chrono::seconds;

  // exponential moving average
  std::vector<UpdateByOperation> result = {
      // rolling sum
      rollingSumTick({"rsum_a = a", "rsum_d = d"}, 10),
      rollingSumTick({ "rsum_a = a", "rsum_d = d"}, 10, 10),
      rollingSumTime("Timestamp", { "rsum_b = b", "rsum_e = e"}, "PT00:00:10"),
      rollingSumTime("Timestamp", { "rsum_b = b", "rsum_e = e"}, secs(10), secs(-10)),
      rollingSumTime("Timestamp", { "rsum_b = b", "rsum_e = e"}, "PT30S", "-PT00:00:20"),
      // rolling group
      rollingGroupTick({ "rgroup_a = a", "rgroup_d = d"}, 10),
      rollingGroupTick({ "rgroup_a = a", "rgroup_d = d"}, 10, 10),
      rollingGroupTime("Timestamp", { "rgroup_b = b", "rgroup_e = e"}, "PT00:00:10"),
      rollingGroupTime("Timestamp", { "rgroup_b = b", "rgroup_e = e"}, secs(10), secs(-10)),
      rollingGroupTime("Timestamp", { "rgroup_b = b", "rgroup_e = e"}, "PT30S", "-PT00:00:20"),
      // rolling average
      rollingAvgTick({ "ravg_a = a", "ravg_d = d"}, 10),
      rollingAvgTick({ "ravg_a = a", "ravg_d = d"}, 10, 10),
      rollingAvgTime("Timestamp", { "ravg_b = b", "ravg_e = e"}, "PT00:00:10"),
      rollingAvgTime("Timestamp", { "ravg_b = b", "ravg_e = e"}, secs(10), secs(-10)),
      rollingAvgTime("Timestamp", { "ravg_b = b", "ravg_e = e"}, "PT30S", "-PT00:00:20"),
      // rolling minimum
      rollingMinTick({ "rmin_a = a", "rmin_d = d"}, 10),
      rollingMinTick({ "rmin_a = a", "rmin_d = d"}, 10, 10),
      rollingMinTime("Timestamp", { "rmin_b = b", "rmin_e = e"}, "PT00:00:10"),
      rollingMinTime("Timestamp", { "rmin_b = b", "rmin_e = e"}, secs(10), secs(-10)),
      rollingMinTime("Timestamp", { "rmin_b = b", "rmin_e = e"}, "PT30S", "-PT00:00:20"),
      // rolling maximum
      rollingMaxTick({ "rmax_a = a", "rmax_d = d"}, 10),
      rollingMaxTick({ "rmax_a = a", "rmax_d = d"}, 10, 10),
      rollingMaxTime("Timestamp", { "rmax_b = b", "rmax_e = e"}, "PT00:00:10"),
      rollingMaxTime("Timestamp", { "rmax_b = b", "rmax_e = e"}, secs(10), secs(-10)),
      rollingMaxTime("Timestamp", { "rmax_b = b", "rmax_e = e"}, "PT30S", "-PT00:00:20"),
      // rolling product
      rollingProdTick({ "rprod_a = a", "rprod_d = d"}, 10),
      rollingProdTick({ "rprod_a = a", "rprod_d = d"}, 10, 10),
      rollingProdTime("Timestamp", { "rprod_b = b", "rprod_e = e"}, "PT00:00:10"),
      rollingProdTime("Timestamp", { "rprod_b = b", "rprod_e = e"}, secs(10), secs(-10)),
      rollingProdTime("Timestamp", { "rprod_b = b", "rprod_e = e"}, "PT30S", "-PT00:00:20"),
      // rolling count
      rollingCountTick({ "rcount_a = a", "rcount_d = d"}, 10),
      rollingCountTick({ "rcount_a = a", "rcount_d = d"}, 10, 10),
      rollingCountTime("Timestamp", { "rcount_b = b", "rcount_e = e"}, "PT00:00:10"),
      rollingCountTime("Timestamp", { "rcount_b = b", "rcount_e = e"}, secs(10), secs(-10)),
      rollingCountTime("Timestamp", { "rcount_b = b", "rcount_e = e"}, "PT30S", "-PT00:00:20"),
      // rolling standard deviation
      rollingStdTick({ "rstd_a = a", "rstd_d = d"}, 10),
      rollingStdTick({ "rstd_a = a", "rstd_d = d"}, 10, 10),
      rollingStdTime("Timestamp", { "rstd_b = b", "rstd_e = e"}, "PT00:00:10"),
      rollingStdTime("Timestamp", { "rstd_b = b", "rstd_e = e"}, secs(10), secs(-10)),
      rollingStdTime("Timestamp", { "rstd_b = b", "rstd_e = e"}, "PT30S", "-PT00:00:20"),
      // rolling weighted average (using "b" as the weight column)
      rollingWavgTick("b", { "rwavg_a = a", "rwavg_d = d"}, 10),
      rollingWavgTick("b", { "rwavg_a = a", "rwavg_d = d"}, 10, 10),
      rollingWavgTime("Timestamp", "b", { "rwavg_b = b", "rwavg_e = e"}, "PT00:00:10"),
      rollingWavgTime("Timestamp", "b", { "rwavg_b = b", "rwavg_e = e"}, secs(10), secs(-10)),
      rollingWavgTime("Timestamp", "b", { "rwavg_b = b", "rwavg_e = e"}, "PT30S", "-PT00:00:20")
  };
return result;
}
