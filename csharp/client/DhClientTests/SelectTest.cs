using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.Utility;
using Xunit.Abstractions;
using Xunit.Sdk;
using System.Xml.Linq;

namespace Deephaven.DhClientTests;

public class SelectTest {
  private readonly ITestOutputHelper _output;

  public SelectTest(ITestOutputHelper output) {
    _output = output;
    PlatformUtf16.Init();
  }

  [Fact]
  public void TestSupportAllTypes() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());

    var boolData = new List<bool>();
    var charData = new List<char>();
    var byteData = new List<sbyte>();
    var shortData = new List<short>();
    var intData = new List<int>();
    var longData = new List<long>();
    var floatData = new List<float>();
    var doubleData = new List<double>();
    var stringData = new List<string>();
    var dateTimeData = new List<DateTime>();

    const int startValue = -8;
    const int endValue = 8;
    for (var i = startValue; i != endValue; ++i) {
      boolData.Add((i % 2) == 0);
      charData.Add((char)(i * 10));
      byteData.Add((sbyte)(i * 11));
      shortData.Add((short)(i * 1000));
      intData.Add(i * 1_000_000);
      longData.Add(i * (long)1_000_000_000);
      floatData.Add(i * 123.456F);
      doubleData.Add(i * 987654.321);
      stringData.Add($"test {i}");
      dateTimeData.Add(DateTimeOffset.FromUnixTimeMilliseconds(i * 1000).DateTime);
    }

    using var maker = new TableMaker();
    maker.AddColumn("boolData", boolData);
    maker.AddColumn("charData", charData);
    maker.AddColumn("byteData", byteData);
    maker.AddColumn("shortData", shortData);
    maker.AddColumn("intData", intData);
    maker.AddColumn("longData", longData);
    maker.AddColumn("floatData", floatData);
    maker.AddColumn("doubleData", doubleData);
    maker.AddColumn("stringData", stringData);
    maker.AddColumn("dateTimeData", dateTimeData);

    var t = maker.MakeTable(ctx.Client.Manager);

    _output.WriteLine(t.ToString(true));

    CompareTable(
      t,
      "boolData", boolData,
      "charData", charData,
      "byteData", byteData,
      "shortData", shortData,
      "intData", intData,
      "longData", longData,
      "floatData", floatData,
      "doubleData", doubleData,
      "stringData", stringData,
      "dateTimeData", dateTimeData
    );
  }

  [Fact]
  public void TestCreateUpdateFetchATable() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());

    var intData = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    var doubleData = new[] { 0.0, 1.1, 2.2, 3.3, 4.4, 5.5, 6.6, 7.7, 8.8, 9.9 };
    var stringData = new[] {
      "zero", "one", "two", "three", "four", "five", "six", "seven",
      "eight", "nine"};

    using var maker = new TableMaker();
    maker.AddColumn("IntValue", intData);
    maker.AddColumn("DoubleValue", doubleData);
    maker.AddColumn("StringValue", stringData);
    var t = maker.MakeTable(ctx.Client.Manager);
    var t2 = t.Update("Q2 = IntValue * 100");
    var t3 = t2.Update("Q3 = Q2 + 10");
    var t4 = t3.Update("Q4 = Q2 + 100");

    var q2Data = new[] { 0, 100, 200, 300, 400, 500, 600, 700, 800, 900 };
    var q3Data = new[] { 10, 110, 210, 310, 410, 510, 610, 710, 810, 910 };
    var q4Data = new[] { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };

    CompareTable(
      t4,
      "IntValue", intData,
      "DoubleValue", doubleData,
      "StringValue", stringData,
      "Q2", q2Data,
      "Q3", q3Data,
      "Q4", q4Data
    );
  }

  TEST_CASE("Select a few columns", "[select]") {
    auto tm = TableMakerForTests::Create();
    auto table = tm.Table();

    auto t1 = table.Where("ImportDate == `2017-11-01` && Ticker == `AAPL`")
        .Select("Ticker", "Close", "Volume")
        .Head(2);

    std::vector < std::string> ticker_data = { "AAPL", "AAPL"};
    std::vector<double> close_data = { 23.5, 24.2 };
    std::vector<int64_t> vol_data = { 100000, 250000 };

    CompareTable(
        t1,
        "Ticker", ticker_data,
        "Close", close_data,
        "Volume", vol_data
    );
  }

  TEST_CASE("LastBy + Select", "[select]") {
    auto tm = TableMakerForTests::Create();
    auto table = tm.Table();

    auto t1 = table.Where("ImportDate == `2017-11-01` && Ticker == `AAPL`")
        .LastBy("Ticker")
        .Select("Ticker", "Close", "Volume");
    std::cout << t1.Stream(true) << '\n';

    std::vector < std::string> ticker_data = { "AAPL"};
    std::vector<double> close_data = { 26.7 };
    std::vector<int64_t> vol_data = { 19000 };

    CompareTable(
        t1,
        "Ticker", ticker_data,
        "Close", close_data,
        "Volume", vol_data
    );
  }

  TEST_CASE("New columns", "[select]") {
    auto tm = TableMakerForTests::Create();
    auto table = tm.Table();

    // A formula expression
    auto t1 = table.Where("ImportDate == `2017-11-01` && Ticker == `AAPL`")
        .Select("MV1 = Volume * Close", "V_plus_12 = Volume + 12");

    std::vector<double> mv1_data = { 2350000, 6050000, 507300 };
    std::vector<int64_t> mv2_data = { 100012, 250012, 19012 };

    CompareTable(
        t1,
        "MV1", mv1_data,
        "V_plus_12", mv2_data
    );
  }

  TEST_CASE("Drop columns", "[select]") {
    auto tm = TableMakerForTests::Create();
    auto table = tm.Table();

    auto t1 = table.DropColumns({ "ImportDate", "Open", "Close"});
    CHECK(2 == t1.Schema()->NumCols());
  }

  TEST_CASE("Simple Where", "[select]") {
    auto tm = TableMakerForTests::Create();
    auto table = tm.Table();
    auto updated = table.Update("QQQ = i");

    auto t1 = updated.Where("ImportDate == `2017-11-01` && Ticker == `IBM`")
        .Select("Ticker", "Volume");

    std::vector < std::string> ticker_data = { "IBM"};
    std::vector<int64_t> vol_data = { 138000 };

    CompareTable(
        t1,
        "Ticker", ticker_data,
        "Volume", vol_data
    );
  }

  TEST_CASE("Formula in the Where clause", "[select]") {
    auto tm = TableMakerForTests::Create();
    auto table = tm.Table();

    auto t1 = table.Where(
            "ImportDate == `2017-11-01` && Ticker == `AAPL` && Volume % 10 == Volume % 100")
        .Select("Ticker", "Volume");
    std::cout << t1.Stream(true) << '\n';

    std::vector < std::string> ticker_data = { "AAPL", "AAPL", "AAPL"};
    std::vector<int64_t> vol_data = { 100000, 250000, 19000 };

    CompareTable(
        t1,
        "Ticker", ticker_data,
        "Volume", vol_data
    );
  }

  TEST_CASE("Simple 'Where' with syntax error", "[select]") {
    auto tm = TableMakerForTests::Create();
    auto table = tm.Table();

    try {
      // String literal
      auto t1 = table.Where(")))))");
      std::cout << t1.Stream(true) << '\n';
    } catch (const std::exception &e) {
      // Expected
      fmt::print(std::cerr, "Caught *expected* exception {}\n", e.what());
      return;
    }
    throw std::runtime_error("Expected a failure, but didn't experience one");
    }

    TEST_CASE("WhereIn", "[select]") {
      auto tm = TableMakerForTests::Create();

      std::vector < std::string> letter_data = { "A", "C", "F", "B", "E", "D", "A"};
      std::vector<std::optional<int32_t>> number_data = { { }, 2, 1, { }, 4, 5, 3 };
      std::vector < std::string> color_data = { "red", "blue", "orange", "purple", "yellow", "pink", "blue"};
      std::vector<std::optional<int32_t>> code_data = { 12, 13, 11, { }, 16, 14, { } };
      TableMaker source_maker;
      source_maker.AddColumn("Letter", letter_data);
      source_maker.AddColumn("Number", number_data);
      source_maker.AddColumn("Color", color_data);
      source_maker.AddColumn("Code", code_data);
      auto source = source_maker.MakeTable(tm.Client().GetManager());

      std::vector < std::string> filter_color_data = { "blue", "red", "purple", "white"};
      TableMaker filter_maker;
      filter_maker.AddColumn("Colors", filter_color_data);
      auto filter = filter_maker.MakeTable(tm.Client().GetManager());

      auto result = source.WhereIn(filter, { "Color = Colors"});

      std::vector < std::string> letter_expected = { "A", "C", "B", "A"};
      std::vector<std::optional<int32_t>> number_expected = { { }, 2, { }, 3 };
      std::vector < std::string> color_expected = { "red", "blue", "purple", "blue"};
      std::vector<std::optional<int32_t>> code_expected = { 12, 13, { }, { } };

      CompareTable(result,
          "Letter", letter_expected,
          "Number", number_expected,
          "Color", color_expected,
          "Code", code_expected);
    }

    TEST_CASE("LazyUpdate", "[select]") {
      auto tm = TableMakerForTests::Create();

      std::vector < std::string> a_data = { "The", "At", "Is", "On"};
      std::vector<int32_t> b_data = { 1, 2, 3, 4 };
      std::vector<int32_t> c_data = { 5, 2, 5, 5 };
      TableMaker source_maker;
      source_maker.AddColumn("A", a_data);
      source_maker.AddColumn("B", b_data);
      source_maker.AddColumn("C", c_data);
      auto source = source_maker.MakeTable(tm.Client().GetManager());

      auto result = source.LazyUpdate({ "Y = sqrt(C)"});

      std::vector<double> sqrt_data = { std::sqrt(5), std::sqrt(2), std::sqrt(5), std::sqrt(5) };
      CompareTable(result,
          "A", a_data,
          "B", b_data,
          "C", c_data,
          "Y", sqrt_data);
    }

    TEST_CASE("SelectDistinct", "[select]") {
      auto tm = TableMakerForTests::Create();

      std::vector < std::string> a_data = { "apple", "apple", "orange", "orange", "plum", "grape"};
      std::vector<int32_t> b_data = { 1, 1, 2, 2, 3, 3 };
      TableMaker source_maker;
      source_maker.AddColumn("A", a_data);
      source_maker.AddColumn("B", b_data);
      auto source = source_maker.MakeTable(tm.Client().GetManager());

      auto result = source.SelectDistinct({ "A"});

      std::cout << result.Stream(true) << '\n';

      std::vector < std::string> expected_data = { "apple", "orange", "plum", "grape"};

      CompareTable(result,
          "A", expected_data);
    }

    private static void CompareTable(TableHandle table, params object[] args) {
    if (args.Length % 2 != 0) {
      throw new ArgumentException($"args array expected to have even number of elements, but has {args.Length}");
    }

    var expectedNumColumns = args.Length / 2;

    var clientTable = table.ToClientTable();
    var actualNumColumns = clientTable.NumColumns;

    if (expectedNumColumns != actualNumColumns) {
      throw new ArgumentException($"Expected {expectedNumColumns}, have {actualNumColumns} columns");
    }

    var nextIndex = 0;
    var cols = new Dictionary<string, IList>();
    foreach (var colName in clientTable.ColumnNames) {
      var column = clientTable.GetColumn(nextIndex);
      cols[colName] = column;
      ++nextIndex;
    }

    for (var colNum = 0; colNum < expectedNumColumns; ++colNum) {
      var expectedColName = (string)args[colNum * 2];
      var expectedColumn = (IList)args[colNum * 2 + 1];
      if (!cols.TryGetValue(expectedColName, out var actualColumn)) {
        throw new ArgumentException($"Table does not have expected column {expectedColName}");
      }

      if (!EnumerablesEqual(expectedColumn, actualColumn)) {
        throw new ArgumentException($"Expected column {EnumerableToString(expectedColumn)} differs from actual column {EnumerableToString(actualColumn)}");
      }
    }
  }

  private static bool EnumerablesEqual(IEnumerable lhs, IEnumerable rhs) {
    var rEnum = rhs.GetEnumerator();
    try {
      foreach (var lItem in lhs) {
        if (!rEnum.MoveNext()) {
          return false;
        }

        if (!object.Equals(lItem, rEnum.Current)) {
          return false;
        }
      }

      return !rEnum.MoveNext();
    } finally {
      if (rEnum is IDisposable id) {
        id.Dispose();
      }
    }
  }

  private static string EnumerableToString(IEnumerable enumerable) {
    var sw = new StringWriter();
    var comma = "";
    foreach (var item in enumerable) {
      sw.Write(comma);
      sw.Write(item);
      comma = ", ";
    }
    return sw.ToString();
  }
}
