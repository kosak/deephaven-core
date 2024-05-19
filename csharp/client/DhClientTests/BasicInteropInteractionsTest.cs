using System.ComponentModel;
using System.Net.WebSockets;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DhClientTests;

public class BasicInteropInteractionsTest {
  public BasicInteropInteractionsTest() {
    PlatformUtf16.Init();
  }

  [Fact]
  public void TestAdd() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Add(3, 4, out var result);
    Assert.Equal(7, result);
  }

  [Fact]
  public void TestConcat() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Concat("Deep🎔", "haven", out var result);
    Assert.Equal("Deep🎔haven", result);
  }

  [Fact]
  public void TestBasicStruct() {
    var bs = new BasicInteropInteractions.BasicStruct(100, "hi");
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_BasicStruct(ref bs, 123, " 🧠 there", out var result);
    Assert.Equal(223, result.i);
    Assert.Equal("hi 🧠 there", result.s);
  }

  [Fact]
  public void TestArrayRunningSum() {
    var data = new [] { 10, 20, 30, 40, 50 };
    var result = new int[5];
    var expectedResult = new[] { 10, 30, 60, 100, 150 };
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_ArrayRunningSum(data, data.Length, result);

    Assert.Equal(expectedResult, result);
  }

  [Fact]
  public void TestCompare() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Less(4, 7, out var result1);
    Assert.True((bool)result1);

    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Less(7, 4, out var result2);
    Assert.False((bool)result2);
  }

  [Fact]
  public void TestCompareArrays() {
    const int length = 3;
    var data1 = new Int32[length] { -3, 8, 55 };
    var data2 = new Int32[length] { -7, 10, 100 };
    var results = new InteropBool[length];
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Less_Array(data1, data2, length, results);

    var expectedResults = new InteropBool[length] { (InteropBool)false, (InteropBool)true, (InteropBool)true };
    Assert.Equal(expectedResults, results);
  }
}
