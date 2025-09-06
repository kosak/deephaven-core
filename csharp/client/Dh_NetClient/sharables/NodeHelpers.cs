//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient.Sharables;

using System.Runtime.CompilerServices;

public interface INode<TSelf> where TSelf : class {
  (TSelf, TSelf, TSelf) CalcDifference(TSelf target, TSelf empty) {
    if (this == target) {

    }
  }
  void GatherNodesForUnitTesting(HashSet<object> nodes);
}

[InlineArray(64)]
public struct Array64<T> {
  public T Item;
}
