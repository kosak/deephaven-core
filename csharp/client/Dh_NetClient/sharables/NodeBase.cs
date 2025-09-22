//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

using System.Runtime.CompilerServices;

public abstract class NodeBase {
  public abstract MutableNode AsMutable();
}

public static class ItemWithCount {
  public static ItemWithCount<T> Of<T>(T item, int count) {
    return new ItemWithCount<T>(item, count);
  }
}

public readonly record struct ItemWithCount<T>(T Item, int Count);

[InlineArray(64)]
public struct Array64<T> {
  public T Item;

  public int Length => 64;
}
