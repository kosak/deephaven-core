using Deephaven.DeephavenClient;
using System;
using Deephaven.DeephavenClient.Utility;

namespace Deephaven.DhClientTests;

public class SortTest {
  [Fact]
  public void SortDemoTable() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var testTable = ctx.TestTable;

    // Limit by date and ticker
    var filtered = testTable.Where("ImportDate == `2017-11-01`")
      .Where("Ticker >= `X`")
      .Select("Ticker", "Open", "Volume");

    var table1 = filtered.Sort(SortPair.Descending("Ticker"), SortPair.Ascending("Volume"));

    var tickerData = new[] { "ZNGA", "ZNGA", "XYZZY", "XRX", "XRX"};
    var openData = new[] { 541.2, 685.3, 92.3, 50.5, 83.1 };
    var volData = new[] { 46123, 48300, 6060842, 87000, 345000 };

    var tc = new TableComparer();
    tc.AddColumn("Ticker", tickerData);
    tc.AddColumn("Open", openData);
    tc.AddColumn("Volume", volData);
    tc.AssertEqualTo(table1);
  }

  [Fact]
  public void SortTempTable() {
    auto tm = TableMakerForTests::Create();

    std::vector<int32_t> int_data0{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15};
    std::vector<int32_t> int_data1{ 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7};
    std::vector<int32_t> int_data2{ 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3};
    std::vector<int32_t> int_data3{ 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1};

    TableMaker maker;
    maker.AddColumn("IntValue0", int_data0);
    maker.AddColumn("IntValue1", int_data1);
    maker.AddColumn("IntValue2", int_data2);
    maker.AddColumn("IntValue3", int_data3);

    auto temp_table = maker.MakeTable(tm.Client().GetManager());

    auto sorted = temp_table.Sort(SortPair::Descending("IntValue3"), SortPair::Ascending("IntValue2"));

    std::vector < std::string> import_date_data = { "2017-11-01", "2017-11-01", "2017-11-01"};
    std::vector < std::string> ticker_data = { "AAPL", "AAPL", "AAPL"};
    std::vector<double> open_data = { 22.1, 26.8, 31.5 };
    std::vector<double> close_data = { 23.5, 24.2, 26.7 };
    std::vector<int64_t> vol_data = { 100000, 250000, 19000 };

    std::vector<int32_t> sid0{ 8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6, 7};
    std::vector<int32_t> sid1{ 4, 4, 5, 5, 6, 6, 7, 7, 0, 0, 1, 1, 2, 2, 3, 3};
    std::vector<int32_t> sid2{ 2, 2, 2, 2, 3, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1};
    std::vector<int32_t> sid3{ 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0};

    CompareTable(
      sorted,
      "IntValue0", sid0,
      "IntValue1", sid1,
      "IntValue2", sid2,
      "IntValue3", sid3
    );
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var testTable = ctx.TestTable;

    using var table = testTable.Where("ImportDate == `2017-11-01`");

    // Run a merge by fetching two tables and them merging them
    var aaplTable = table.Where("Ticker == `AAPL`").Tail(10);
    var zngaTable = table.Where("Ticker == `ZNGA`").Tail(10);

    var merged = aaplTable.Merge(new[] { zngaTable });

    var importDateData = new[] {
      "2017-11-01", "2017-11-01", "2017-11-01",
      "2017-11-01", "2017-11-01"
    };
    var tickerData = new[] { "AAPL", "AAPL", "AAPL", "ZNGA", "ZNGA" };
    var openData = new[] { 22.1, 26.8, 31.5, 541.2, 685.3 };
    var closeData = new[] { 23.5, 24.2, 26.7, 538.2, 544.9 };
    var volData = new Int64[] { 100000, 250000, 19000, 46123, 48300 };

    var tc = new TableComparer();
    tc.AddColumn("ImportDate", importDateData);
    tc.AddColumn("Ticker", tickerData);
    tc.AddColumn("Open", openData);
    tc.AddColumn("Close", closeData);
    tc.AddColumn("Volume", volData);
    tc.AssertEqualTo(merged);
  }
}
