//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

public interface IAmImmutable<TSelf> {
  /// <summary>
  /// This method exists as a hack to get the static Empty instance for this type.
  /// To do this, callers can say new T().GetEmptyInstanceForThisType().
  /// The "new T()" may feel wasteful but it is only done when creating the chain
  /// of Empty instances for a given T, and not repeatedly run in the steady state.
  /// </summary>
  /// <returns></returns>
  public TSelf GetEmptyInstanceForThisType();
  public (ItemWithCount<TSelf>, ItemWithCount<TSelf>, ItemWithCount<TSelf>) CalcDifference(
    ItemWithCount<TSelf> self, ItemWithCount<TSelf> target);
  public void GatherNodesForUnitTesting(HashSet<object> nodes);
}
