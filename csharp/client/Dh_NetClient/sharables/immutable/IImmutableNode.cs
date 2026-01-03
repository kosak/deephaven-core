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
public interface IImmutableNode<TSelf> where TSelf : struct {
  /// <summary>
  /// This method exists as a hack to get the static Empty instance for this type.
  /// To do this, callers can say new T().EmptyInstance.
  /// The "new T()" may feel wasteful but it is only done when creating the chain
  /// of Empty instances for a given T, and not repeatedly run in the steady state.
  /// </summary>
  /// <returns></returns>
  public TSelf GetEmptyInstance();
  /// <summary>
  /// The number of values in the subtree rooted here. Values are stored in ImmutableLeaf.
  /// </summary>
  public int Count { get; }
  /// <summary>
  /// Calculate the differences between subtrees 'this' and 'after'
  /// (where 'this' can be considered 'before').
  /// The differences are returned as the triple of trees (added, removed, modified).
  /// 'added' is interpreted as: items to be added to 'this' to form 'after'
  /// 'removed' is interpreted as: items to be removed from 'this' to form 'after'
  /// 'modified' is interpreted as: items (values) to be modified in 'this' to form 'after'
  /// </summary>
  /// <param name="after"></param>
  /// <returns></returns>
  public (TSelf, TSelf, TSelf) CalcDifference(TSelf after);
  /// <summary>
  /// Used in unit tests to count nodes.
  /// </summary>
  /// <param name="nodes"></param>
  public void GatherNodesForUnitTesting(HashSet<object> nodes);
}
