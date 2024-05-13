using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.Utility;
using Xunit.Abstractions;
using Xunit.Sdk;

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
