//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

/// <summary>
/// This interface exists as a type constraint for ImmutableInternal and ImmutableLeaf.
/// Those are value types, and their methods and properties are accesses directly.
/// In particular, we don't ever box them or dispatch through the interface.
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public interface IImmutableNode<TSelf> where TSelf : class {
  /// <summary>
  /// This method exists as a hack to get the static Empty instance for this type.
  /// To do this, callers can say new T().GetEmptyInstanceForThisType().
  /// The "new T()" may feel wasteful but it is only done when creating the chain
  /// of Empty instances for a given T, and not repeatedly run in the steady state.
  /// </summary>
  /// <returns></returns>
  public TSelf GetEmptyInstanceForThisType();
  public (TSelf, TSelf, TSelf) CalcDifference(TSelf target);
  public void GatherNodesForUnitTesting(HashSet<object> nodes);
  public int Count { get; }
}
