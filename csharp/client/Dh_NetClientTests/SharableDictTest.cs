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

    Assert.False(d1.TryGetValue(1001, out _));
  }

  [Fact]
  public void Canonicalizes() {
    var d0 = SharableDict<string>.Empty;
    var d1 = d0.With(1_000, "hello");
    var d2 = d1.With(1_000_000, "there");
    var d3 = d2.With(1_000_000_000, "Deephaven");

    Assert.Equal(0, d0.Count);
    Assert.Equal(1, d1.Count);
    Assert.Equal(2, d2.Count);
    Assert.Equal(3, d3.Count);

    var newd2 = d3.Without(1_000);
    var newd1 = newd2.Without(1_000_000);
    var newd0 = newd1.Without(1_000_000_000);

    Assert.True(ReferenceEquals(newd0.Root, d0.Root));
  }
}
