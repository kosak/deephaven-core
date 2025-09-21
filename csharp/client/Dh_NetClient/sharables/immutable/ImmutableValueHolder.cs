//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

/// <summary>
/// This struct exists at the leaf of the immutable tree. As a struct, it takes up the same amount
/// of space as 'TValue'. Its immediate parent holds an array of 64 of these. Because this is a
/// struct, the parent array holds these directly as values, rather than being references to
/// heap-allocated objects. This allows us to use memory more efficiently. Put another way,
/// our immediate parent has 64 values packed into it, rather than 64 references to values.
/// </summary>
/// <typeparam name="TValue"></typeparam>
public readonly struct ImmutableValueHolder<TValue> : IAmImmutable<ImmutableValueHolder<TValue>> {
  public readonly TValue Value;

  public ImmutableValueHolder(TValue value) {
    Value = value;
  }

  public ImmutableValueHolder<TValue> GetEmptyInstanceForThisType() {
    // We are a struct, so we don't have a singleton canonical instance.
    return new();
  }

  public (ItemWithCount<ImmutableValueHolder<TValue>>,
    ItemWithCount<ImmutableValueHolder<TValue>>,
    ItemWithCount<ImmutableValueHolder<TValue>>)
    CalcDifference(
    ItemWithCount<ImmutableValueHolder<TValue>> self,
    ItemWithCount<ImmutableValueHolder<TValue>> target) {
    var empty = ItemWithCount.Of(new ImmutableValueHolder<TValue>(), 0);
    switch (self.Count, target.Count) {
      case (0, 0): {
          // Both sides empty
          return (empty, empty, empty);  // added, removed, modified
        }
      case (0, 1): {
          // Source empty, target exists
          return (target, empty, empty);  // added, removed, modified
        }
      case (1, 0): {
          // Target empty, source exists
          return (empty, self, empty);  // added, removed modified
        }
      case (1, 1): {
          // Both exist. Compare values compare.
          if (Equals(Value, target.Item.Value)) {
            // Same
            return (empty, empty, empty); // added, removed, modified
          }
          return (empty, empty, target);  // added, removed modified
        }
      default: {
          throw new Exception($"Assertion failure: thisCount={self.Count}, targetCount={target.Count}");
        }
    }
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    // Do nothing.
  }
}
