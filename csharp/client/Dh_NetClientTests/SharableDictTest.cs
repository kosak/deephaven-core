//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
using Deephaven.Dh_NetClient;

namespace Deephaven.Dh_NetClientTests;

public class SharableDictTest {
  [Fact]
  public void Simple() {
    var d = SharableDict<string>.Empty;
    var dict = d.With(10, "hello")
      .With(11, "world")
      .With(1000, "Deephaven");

    Assert.True(DictContains(dict, 10, "hello"));
    Assert.True(DictContains(dict, 11, "world"));
    Assert.True(DictContains(dict, 1000, "Deephaven"));
    Assert.False(dict.TryGetValue(1001, out _));
    Assert.Equal(3, dict.Count);

    var dict2 = dict.With(11, "world v2")
      .With(1000, "Deephaven v2");

    // Dict has some new values
    Assert.True(DictContains(dict2, 10, "hello"));
    Assert.True(DictContains(dict2, 11, "world v2"));
    Assert.True(DictContains(dict2, 1000, "Deephaven v2"));
    Assert.Equal(3, dict2.Count);

    // Initial dict unchanged
    Assert.True(DictContains(dict, 10, "hello"));
    Assert.True(DictContains(dict, 11, "world"));
    Assert.True(DictContains(dict, 1000, "Deephaven"));
    Assert.Equal(3, dict.Count);
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

    Assert.True(ReferenceEquals(newd0.RootForUnitTests, d0.RootForUnitTests));
  }

  [Fact]
  public void Iterates() {
    var dict = SharableDict<string>.Empty;
    for (var i = 0; i != 10000; ++i) {
      dict = dict.With(i * 37, "hello" + i);
    }

    var nextIndex = 0;
    foreach (var (k, v) in dict) {
      Assert.Equal(nextIndex * 37, k);
      Assert.Equal("hello" + nextIndex, v);
      ++nextIndex;
    }
  }

  [Fact]
  public void DictIsEfficientForLargeDenseSets() {
    var dict = SharableDict<string>.Empty;
    var temp = dict.CountNodesForUnitTesting();
    Assert.Equal(10, temp);
  }

  private static bool DictContains<T>(SharableDict<T> dict, Int64 key, T expected) {
    return dict.TryGetValue(key, out var value) && Object.Equals(value, expected);
  }
}
