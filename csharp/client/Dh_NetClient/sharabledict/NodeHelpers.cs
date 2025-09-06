//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

using System.Runtime.CompilerServices;

public interface INode<TSelf> {
  int Count { get; }
  (TSelf, TSelf, TSelf) CalcDifference(int depth, TSelf target, TSelf emptySubtree);
  void GatherNodesForUnitTesting(HashSet<object> nodes);
}

[InlineArray(64)]
public struct Array64<T> {
  public T Item;
}
