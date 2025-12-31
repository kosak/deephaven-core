//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

using System.Runtime.CompilerServices;

public abstract class NodeBase<TChild> {
  protected int _count = 0;

  public int Count => _count;

  public abstract MutableNode<TChild> AsMutable();
}

[InlineArray(64)]
public struct Array64<T> {
  public T Item;

  public int Length => 64;
}
