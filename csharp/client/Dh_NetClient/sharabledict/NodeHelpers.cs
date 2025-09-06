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

public struct ValueWrapper<T> : INode<ValueWrapper<T>> {
  public readonly T Value;

  public ValueWrapper(T value) {
    Value = value;
  }

  public int Count => throw new NotImplementedException();

  public (ValueWrapper<T>, ValueWrapper<T>, ValueWrapper<T>) CalcDifference(
    int depth, ValueWrapper<T> target, ValueWrapper<T> empty) {
    throw new NotImplementedException();
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    // Do nothing
  }
}

[InlineArray(64)]
public struct Array64<T> {
  public T Item;
}
