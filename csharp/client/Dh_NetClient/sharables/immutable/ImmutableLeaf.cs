//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics.CodeAnalysis;

namespace Deephaven.Dh_NetClient;

/// <summary>
/// This struct is a value type with two members: a count, and a reference to an array of values.
/// </summary>
public readonly struct ImmutableLeaf<TValue> : IImmutableNode<ImmutableLeaf<TValue>> {
  // TODO(kosak): put somewhere
  private const int NumChildren = 64;

  public static readonly ImmutableLeaf<TValue> EmptyInstance = new();

  public ImmutableLeaf<TValue> GetEmptyInstance() => EmptyInstance;

  public static ImmutableLeaf<TValue> Of(Bitset64 validitySet, TValue[] children) {
    return validitySet.IsEmpty ? EmptyInstance : new ImmutableLeaf<TValue>(validitySet, children);
  }

  public readonly Bitset64 ValiditySet;
  public readonly TValue[] Children;

  public int Count => ValiditySet.Count;

  private ImmutableLeaf(Bitset64 validitySet, TValue[] children) {
    ValiditySet = validitySet;
    Children = children;
  }

  public ImmutableLeaf<TValue> With(int index, TValue value) {
    var newVs = ValiditySet.WithElement(index);
    var newChildren = new TValue[NumChildren];
    ((ReadOnlySpan<TValue>)Children).CopyTo(newChildren);
    newChildren[index] = value;
    return new ImmutableLeaf<TValue>(newVs, newChildren);
  }

  public ImmutableLeaf<TValue> Without(int index) {
    var newVs = ValiditySet.WithoutElement(index);
    if (newVs.IsEmpty) {
      return EmptyInstance;
    }
    var newChildren = new TValue[NumChildren];
    ((ReadOnlySpan<TValue>)Children).CopyTo(newChildren);
    newChildren[index] = default;
    return new ImmutableLeaf<TValue>(newVs, newChildren);
  }

  public bool TryGetChild(int childIndex, [MaybeNullWhen(false)] out TValue child) {
    if (!ValiditySet.ContainsElement(childIndex)) {
      child = default;
      return false;
    }
    child = Children[childIndex];
    return true;
  }

  public (ImmutableLeaf<TValue>, ImmutableLeaf<TValue>, ImmutableLeaf<TValue>) CalcDifference(
    ImmutableLeaf<TValue> after) {
    var empty = EmptyInstance;
    if (ReferenceEquals(this.Children, after.Children)) {
      // Source and target are the same. No changes
      return (empty, empty, empty); // added, removed, modified
    }
    if (this.Count == 0) {
      // Relative to an empty source, everything in target was added
      return (after, empty, empty); // added, removed, modified
    }
    if (after.Count == 0) {
      // Relative to an empty destination, everything in src was removed
      return (empty, this, empty); // added, removed, modified
    }

    var addedValues = new TValue[NumChildren];
    var removedValues = new TValue[NumChildren];
    var modifiedValues = new TValue[NumChildren];

    var addedSet = new Bitset64();
    var removedSet = new Bitset64();
    var modifiedSet = new Bitset64();

    var union = ValiditySet.Union(after.ValiditySet);

    while (union.TryExtractLowestBit(out var nextUnion, out var i)) {
      union = nextUnion;

      var selfHasBit = ValiditySet.ContainsElement(i);
      var targetHasBit = after.ValiditySet.ContainsElement(i);

      switch (selfHasBit, targetHasBit) {
        case (true, true): {
          // self && target. This is a modify (if the values are different) or a no-op (if they are the same)
          if (!Object.Equals(Children[i], after.Children[i])) {
            modifiedValues[i] = after.Children[i];
            modifiedSet = modifiedSet.WithElement(i);
          }
          break;
        }

        case (true, false): {
          removedValues[i] = Children[i];
          removedSet = removedSet.WithElement(i);
          break;
        }

        case (false, true): {
          addedValues[i] = after.Children[i];
          addedSet = addedSet.WithElement(i);
          break;
        }

        case (false, false): {
          // can't happen
          throw new Exception("Assertion failure in CalcDifference");
        }
      }
    }

    var aResult = Of(addedSet, addedValues);
    var rResult = Of(removedSet, removedValues);
    var mResult = Of(modifiedSet, modifiedValues);
    return (aResult, rResult, mResult);
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    nodes.Add(Children);
  }
}
