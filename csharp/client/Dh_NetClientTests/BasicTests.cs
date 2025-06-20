using Xunit.Abstractions;
using System;
using Deephaven.DhClientTests;
using Deephaven.ManagedClient;

namespace Deephaven.Dh_NetClientTests;

public class BasicTests {
  private readonly ITestOutputHelper _output;

  public BasicTests(ITestOutputHelper output) {
    _output = output;
  }

  [Fact]
  public void TestClosePlaysNiceWithDispose() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;
    using var updated = table.Update("QQQ = i");
    ctx.Client.Close();
  }
}
