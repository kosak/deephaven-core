//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
using Deephaven.Dh_NetClient;

namespace Deephaven.Dh_NetClientTests;

public class SharableDictTest {
  [Fact]
  public void Simple() {
    var d = SharableDict<string>.Empty;
    var d1 = d.With(10, "hello")
      .With(11, "world")
      .With(1000, "Deephaven");

    Assert.True(d1.TryGetValue(10, out var value));
    Assert.Equal("hello", value);

    Assert.True(d1.TryGetValue(11, out value));
    Assert.Equal("world", value);

    Assert.True(d1.TryGetValue(1000, out value));
    Assert.Equal("Deephaven", value);

    Assert.False(d1.TryGetValue(1001, out value));
  }
}
