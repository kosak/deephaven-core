using System.Net.WebSockets;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DhClientTests;

public class BasicInteropTest {
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
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_BasicStruct(100, "hi", 123, "🧠 there",
      out var result);
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
  public void TestArrayElementConcat() {
    var data = new[] { "a", "b", "c", "d", "e" };
    var result = new string[5];
    var expectedResult = new[] { "a🦷", "b🦷", "c🦷", "d🦷", "e🦷" };
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(data, data.Length, "🦷", result);

    Assert.Equal(expectedResult, result);
  }
}
