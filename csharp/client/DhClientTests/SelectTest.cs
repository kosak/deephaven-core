using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.Utility;
using Xunit.Abstractions;
using Xunit.Sdk;
using System.Xml.Linq;
using Microsoft.VisualBasic;

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

  [Fact]
  public void TestSelectAFewColumns() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    var t1 = table.Where("ImportDate == `2017-11-01` && Ticker == `AAPL`")
        .Select("Ticker", "Close", "Volume")
        .Head(2);

    var tickerData = new[] { "AAPL", "AAPL"};
    var closeData = new [] { 23.5, 24.2 };
    var volDdata = new [] { 100000, 250000 };

    CompareTable(
        t1,
        "Ticker", tickerData,
        "Close", closeData,
        "Volume", volDdata
    );
  }

  [Fact]
  public void TestLastByAndSelect() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    var t1 = table.Where("ImportDate == `2017-11-01` && Ticker == `AAPL`")
        .LastBy("Ticker")
        .Select("Ticker", "Close", "Volume");
    _output.WriteLine(t1.ToString(true));

    var tickerData = new[] { "AAPL"};
    var closeData = new[] { 26.7 };
    var volData = new Int64[] { 19000 };

    CompareTable(
        t1,
        "Ticker", tickerData,
        "Close", closeData,
        "Volume", volData
    );
  }

  [Fact]
  public void TestNewColumns() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    // A formula expression
    var t1 = table.Where("ImportDate == `2017-11-01` && Ticker == `AAPL`")
        .Select("MV1 = Volume * Close", "V_plus_12 = Volume + 12");

    var mv1Data = new double[]{ 2350000, 6050000, 507300 };
    var mv2Data = new long[] { 100012, 250012, 19012 };

    CompareTable(
        t1,
        "MV1", mv1Data,
        "V_plus_12", mv2Data
    );
  }

  [Fact]
  public void TestDropColumns() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    var t1 = table.DropColumns("ImportDate", "Open", "Close");
    Assert.Equal(5, table.Schema.NumCols);
    Assert.Equal(2, t1.Schema.NumCols);
  }

  [Fact]
  public void TestSimpleWhere() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;
    var updated = table.Update("QQQ = i");

    var t1 = updated.Where("ImportDate == `2017-11-01` && Ticker == `IBM`")
        .Select("Ticker", "Volume");

    var tickerData = new[] { "IBM"};
    var volData = new Int64[] { 138000 };

    CompareTable(
        t1,
        "Ticker", tickerData,
        "Volume", volData
    );
  }

  [Fact]
  public void TestFormulaInTheWhereClause() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    var t1 = table.Where(
            "ImportDate == `2017-11-01` && Ticker == `AAPL` && Volume % 10 == Volume % 100")
        .Select("Ticker", "Volume");
    _output.WriteLine(t1.ToString(true));

    var tickerData = new string[] { "AAPL", "AAPL", "AAPL"};
    var volData = new Int64[] { 100000, 250000, 19000 };

    CompareTable(
        t1,
        "Ticker", tickerData,
        "Volume", volData
    );
  }

  [Fact]
  public void TestSimpleWhereWithSyntaxError() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    Assert.Throws<Exception>(() => {
      var t1 = table.Where(")))))");
      _output.WriteLine(t1.ToString(true));
    });
  }

  [Fact]
  public void TestWhereIn() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());

    var letterData = new[] { "A", "C", "F", "B", "E", "D", "A" };
    var numberData = new Int32?[] { null, 2, 1, null, 4, 5, 3 };
    var colorData = new string[] { "red", "blue", "orange", "purple", "yellow", "pink", "blue" };
    var codeData = new Int32?[] { 12, 13, 11, null, 16, 14, null };

    using var sourceMaker = new TableMaker();
    sourceMaker.AddColumn("Letter", letterData);
    sourceMaker.AddColumn("Number", numberData);
    sourceMaker.AddColumn("Color", colorData);
    sourceMaker.AddColumn("Code", codeData);

    var source = sourceMaker.MakeTable(ctx.Client.Manager);

    var filterColorData = new[] { "blue", "red", "purple", "white" };

    using var filterMaker = new TableMaker();
    filterMaker.AddColumn("Colors", filterColorData);
    var filter = filterMaker.MakeTable(ctx.Client.Manager);

    var result = source.WhereIn(filter, "Color = Colors");

    var letterExpected = new[] { "A", "C", "B", "A" };
    var numberExpected = new Int32?[] { null, 2, null, 3 };
    var colorExpected = new[] { "red", "blue", "purple", "blue" };
    var codeExpected = new Int32?[] { 12, 13, null, null };

    CompareTable(result,
      "Letter", letterExpected,
      "Number", numberExpected,
      "Color", colorExpected,
      "Code", codeExpected);
  }

  [Fact]
  public void TestLazyUpdate() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());

    var aData = new[] { "The", "At", "Is", "On" };
    var bData = new[] { 1, 2, 3, 4 };
    var cData = new[] { 5, 2, 5, 5 };

    using var sourceMaker = new TableMaker();
    sourceMaker.AddColumn("A", aData);
    sourceMaker.AddColumn("B", bData);
    sourceMaker.AddColumn("C", cData);
    var source = sourceMaker.MakeTable(ctx.Client.Manager);

    var result = source.LazyUpdate("Y = sqrt(C)");

    var sqrtData = new[] { Math.Sqrt(5), Math.Sqrt(2), Math.Sqrt(5), Math.Sqrt(5) };
    CompareTable(result,
      "A", aData,
      "B", bData,
      "C", cData,
      "Y", sqrtData);
  }

  [Fact]
  public void TestSelectDistinct() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());

    var aData = new[] { "apple", "apple", "orange", "orange", "plum", "grape" };
    var bData = new[] { 1, 1, 2, 2, 3, 3 };

    using var sourceMaker = new TableMaker();
    sourceMaker.AddColumn("A", aData);
    sourceMaker.AddColumn("B", bData);
    var source = sourceMaker.MakeTable(ctx.Client.Manager);

    var result = source.SelectDistinct("A");
    _output.WriteLine(result.ToString(true));

    var expectedData = new[] { "apple", "orange", "plum", "grape" };

    CompareTable(result,
      "A", expectedData);
  }

  private static void CompareTable(TableHandle table, params object[] args) {
    if (args.Length % 2 != 0) {
      throw new ArgumentException($"args array expected to have even number of elements, but has {args.Length}");
    }

    var expectedNumColumns = args.Length / 2;

    var clientTable = table.ToClientTable();
    var actualNumColumns = clientTable.NumCols;

    if (expectedNumColumns != actualNumColumns) {
      throw new ArgumentException($"Expected {expectedNumColumns}, have {actualNumColumns} columns");
    }

    for (var colNum = 0; colNum < expectedNumColumns; ++colNum) {
      var expectedColName = (string)args[colNum * 2];
      var expectedColumn = (IList)args[colNum * 2 + 1];
      var actualColIndex = clientTable.Schema.GetColumnIndex(expectedColName);
      var actualColumn = clientTable.GetColumn(actualColIndex);

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
