using Deephaven.Dhe_NetClient;
using Xunit.Abstractions;

namespace Deephaven.Dhe_NetClientTests;

public class Placeholder(ITestOutputHelper output) {
  [Fact]
  public void SillyTest() {
    _ = ClientUtil.GetName("hello");
    output.WriteLine("Make more tests");
  }
}
