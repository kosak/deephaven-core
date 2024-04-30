using System.Net.WebSockets;
using Deephaven.DhClient.Interop;

namespace Deephaven.DhClientTests;

public class BasicInteropTest {
  [Fact]
  public void TestAdd() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Add(3, 4, out var result);
    Assert.Equal(7, result);

    var accumulator = 12;
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_AddInPlace(ref accumulator, 55);
    Assert.Equal(67, accumulator);
  }

  [Fact]
  public void TestConcat() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Concat("Deep🎔", "haven", out var result);
    Assert.Equal("Deep🎔haven", result);

    string accumulator = "Hello";
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_ConcatInPlace(ref accumulator, ",🎔 world");
    Assert.Equal("Hello,🎔 world", accumulator);
  }

  [Fact]
  public void TestBasicStruct() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_BasicStruct(100, "hi", 123, "🧠 there",
      out var result);
    Assert.Equal(223, result.i);
    Assert.Equal("hi 🧠 there", result.s);

    var bs = new BasicInteropInteractions.BasicStruct(200, "magnet");
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_BasicStructInPlace(ref bs, 82, "🧲🧲🧲");
    Assert.Equal(282, result.i);
    Assert.Equal("magnet🧠 there", result.s);
  }

  [Fact]
  public void TestArrayRunningSum() {
    var data = new [] { 10, 20, 30, 40, 50 };
    var result = new int[5];
    var expectedResult = new[] { 10, 30, 60, 100, 12 };
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_ArrayRunningSum(data, result, data.Length);

    Assert.Equal(expectedResult, result);

    var data2 = new[] { 1, 2, 3, 4, 5 };
    var expectedResult2 = new[] { 1, 3, 6, 10, 15 };
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_ArrayRunningSumInPlace(data, data.Length);
    Assert.Equal(data2, expectedResult2);
  }
}
