using Deephaven.DhClient.Interop;

namespace Deephaven.DhClientTests;

public class BasicInteropTest {
  [Fact]
  public void TestInteropInteractions() {
    {
      BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Add(3, 4, out var result);
      Assert.Equal(7, result);
    }

    {
      BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Concat("Deep??", "haven", out var result);
      Assert.Equal("Deep??haven", result);
    }

    {
      int[] data = { 10, 20, 30, 40, 50 };
      var result = new int[5];
      var expectedResult = new[] { 10, 30, 60, 100, 12 };
      BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_RunningSum(data, data.Length, result);

      Assert.Equal(expectedResult, result);
    }
  }
}
