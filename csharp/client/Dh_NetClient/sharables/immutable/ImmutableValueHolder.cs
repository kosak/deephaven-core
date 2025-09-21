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

  public ((ImmutableValueHolder<TValue>, int),
    (ImmutableValueHolder<TValue>, int),
    (ImmutableValueHolder<TValue>, int)) CalcDifference(
    (ImmutableValueHolder<TValue>, int) self,
    (ImmutableValueHolder<TValue>, int) target) {
    var empty0 = (new ImmutableValueHolder<TValue>(), 0);
    switch (self.Item2, target.Item2) {
      case (0, 0): {
          // Both sides empty
          return (empty0, empty0, empty0);  // added, removed, modified
        }
      case (0, 1): {
          // Source empty, target exists
          return (target, empty0, empty0);  // added, removed, modified
        }
      case (1, 0): {
          // Target empty, source exists
          return (empty0, self, empty0);  // added, removed modified
        }
      case (1, 1): {
          // Both exists. Do compare.
          if (Equals(Value, target.Item1.Value)) {
            // Same
            return (empty0, empty0, empty0); // added, removed, modified
          }
          return (empty0, empty0, target);  // added, removed modified
        }
      default: {
          throw new Exception($"Assertion failure: thisCount={self.Item2}, targetCount={target.Item2}");
        }
    }
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    // Do nothing.
  }
}
