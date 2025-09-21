//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

public abstract class NodeBase {
}

public interface IAmImmutable<TSelf> {
  /// <summary>
  /// This method exists as a hack to get the static Empty instance for this type.
  /// To do this, callers can say new T().GetEmptyInstanceForThisType().
  /// The "new T()" may feel wasteful but it is only done when creating the chain
  /// of Empty instances for a given T, and not repeatedly run in the steady state.
  /// </summary>
  /// <returns></returns>
  public TSelf GetEmptyInstanceForThisType();
  public (TSelf, int, TSelf, int, TSelf, int) CalcDifference(int thisCount,
    TSelf target, int targetCount);
  public void GatherNodesForUnitTesting(HashSet<object> nodes);
}

public struct ImmutableValueHolder<TValue> : IAmImmutable<ImmutableValueHolder<TValue>> {
  public readonly TValue Value;

  public ImmutableValueHolder(TValue value) {
    Value = value;
  }

  public ImmutableValueHolder<TValue> GetEmptyInstanceForThisType() {
    // We are a struct, so we don't have a singleton canonical instance.
    return new();
  }

  public (ImmutableValueHolder<TValue>, int, ImmutableValueHolder<TValue>, int, ImmutableValueHolder<TValue>, int) CalcDifference(int thisCount,
    ImmutableValueHolder<TValue> target, int targetCount) {
    var empty = new ImmutableValueHolder<TValue>();
    switch (thisCount, targetCount) {
      case (0, 0): {
        // Both sides empty
        return (empty, 0, empty, 0, empty, 0);  // added, removed, modified
      }
      case (0, 1): {
        // Source empty, target exists
        return (target, 1, empty, 0, empty, 0);  // added, removed, modified
      }
      case (1, 0): {
        // Target empty, source exists
        return (empty, 0, this, 1, empty, 0);  // added, removed modified
      }
      case (1, 1): {
        // Both exists. Do compare.
        if (Equals(Value, target.Value)) {
          // Same
          return (empty, 0, empty, 0, empty, 0); // added, removed, modified
        }
        return (empty, 0, empty, 0, target, 1);  // added, removed modified
      }
      default: {
        throw new Exception($"Assertion failure: thisCount={thisCount}, targetCount={targetCount}");
      }
    }
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    // Do nothing.
  }
}
