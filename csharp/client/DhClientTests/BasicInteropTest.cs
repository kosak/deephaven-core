using Deephaven.DhClient.Interop;

namespace Deephaven.DhClientTests;

public class BasicInteropTest {
  [Fact]
  public void TestInteropInteractions() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Add(3, 4, out var result1);
    Assert.Equal(7, result1);

    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Concat("Deep", "haven", out var result2);
    Assert.Equal("Deephaven", result2);
  }
}
