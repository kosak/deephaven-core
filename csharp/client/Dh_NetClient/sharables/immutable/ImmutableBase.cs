//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient.Sharables.Immutable;

public abstract class ImmutableBase<TSelf> where TSelf : class {
  public readonly int Count;

  public ImmutableBase(int count) {
    Count = count;
  }

  public abstract (TSelf, TSelf, TSelf) CalcDifference(TSelf target, TSelf empty);
  public abstract void GatherNodesForUnitTesting(HashSet<object> nodes);
}
