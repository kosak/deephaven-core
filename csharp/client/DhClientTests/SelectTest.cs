using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Deephaven.DhClientTests;

public class SelectTest {
  public SelectTest() {
    PlatformUtf16.Init();
  }

  [Fact]
  public void TestSupportAllTypes() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Add(3, 4, out var result);
    Assert.Equal(7, result);

    var tm = TableMakerForTests.Create();

    var boolData = new List<bool>();
    var charData = new List<char>();
    var byteData = new List<byte>();
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
      byteData.Add((byte)(i * 11));
      shortData.Add((short)(i * 1000));
      intData.Add(i * 1_000_000);
      longData.Add(i * 1_000_000_000);
      floatData.Add(i * 123.456F);
      doubleData.Add(i * 987654.321);
      stringData.Add($"test {i}");
      dateTimeData.Add(DateTimeOffset.FromUnixTimeMilliseconds(i * 1000).DateTime);
    }

    TableMaker maker;
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

    var t = maker.MakeTable(tm.Client().GetManager());

    t.Stream(Console.Out, true);

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
}
