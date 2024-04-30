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
      BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Concat("Deep", "haven", out var result);
      Assert.Equal("Deephaven", result);
    }

    {
      int[] data = { 10, 20, 30, 40, 50 };

      int[] result = { 10, 20, 30, 40, 50 };
      BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Add(3, 4, out var result1);
    }
  }
