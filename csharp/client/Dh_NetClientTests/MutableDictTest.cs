//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

using Deephaven.Dh_NetClient;

namespace Deephaven.Dh_NetClientTests;

public class MutableDictTest {
  [Fact]
  public void Simple() {
    var m = SharableDict<string>.Empty.AsMutable();
    m.Add(10, "hello");
    m.Add(11, "world");
    m.Add(1000, "Deephaven");

    var dict = m.AsImmutable();
    Assert.True(ContainsEntry(dict, 10, "hello"));
    Assert.True(ContainsEntry(dict, 11, "world"));
    Assert.True(ContainsEntry(dict, 1000, "Deephaven"));
    Assert.False(dict.TryGetValue(1001, out _));
    Assert.Equal(3, dict.Count);
  }

  private static bool ContainsEntry<T>(SharableDict<T> dict, Int64 key, T expected) {
    return dict.TryGetValue(key, out var value) && Object.Equals(value, expected);
  }
}
