//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics.CodeAnalysis;

namespace Deephaven.Dh_NetClient.Sharabledict.Immutable;

public class ImmutableBase {
  public readonly int Count;

}

public class ImmutableLeaf<TValue> : INode<ImmutableLeaf<TValue>> {
  public static ImmutableLeaf<TValue> OfEmpty(TValue placeholder) {
    var children = new Array64<TValue>();
    ((Span<TValue>)children).Fill(placeholder);
    return new ImmutableLeaf<TValue>(0, new Bitset64(), children);
  }

  public readonly Bitset64 ValiditySet;
  public readonly Array64<TValue> Children;

  private ImmutableLeaf(int count, Bitset64 validitySet, ReadOnlySpan<TValue> children) 
    : base(count) {
    ValiditySet = validitySet;
    children.CopyTo(Children);
  }

  public ImmutableLeaf<TValue> WithLeaf(int index, TValue leaf) {
    var newVs = ValiditySet.WithElement(index);
    var newCount = newVs.Count;
    var newChildren = new Array64<TValue>();
    ((ReadOnlySpan<TValue>)Children).CopyTo(newChildren);
    newChildren[index] = leaf;
    return new ImmutableLeaf<TValue>(newCount, newVs, newChildren);
  }

  public ImmutableLeaf<TValue> WithoutLeaf(int index) {
    var newVs = ValiditySet.WithoutElement(index);
    var newCount = newVs.Count;
    var newChildren = new Array64<TValue>();
    ((ReadOnlySpan<TValue>)Children).CopyTo(newChildren);
    newChildren[index] = placeholderValue;
    return new ImmutableLeaf<TValue>(newCount, newVs, Children);
  }

  public bool TryGetChild(int childIndex, [MaybeNullWhen(false)] out TValue child) {
    if (!ValiditySet.ContainsElement(childIndex)) {
      child = default;
      return false;
    }
    child = Children[childIndex];
    return true;
  }

  public (ImmutableLeaf<TValue>, ImmutableLeaf<TValue>, ImmutableLeaf<TValue>) CalcDifference(int depth,
    ImmutableLeaf<TValue> target, ImmutableLeaf<TValue> empty) {
    if (this == target) {
      // Source and target are the same. No changes
      return (empty, empty, empty); // added, removed, modified
    }
    if (this == empty) {
      // Relative to an empty source, everything in target was added
      return (target, empty, empty); // added, removed, modified
    }
    if (target == empty) {
      // Relative to an empty destination, everything in src was removed
      return (empty, this, empty); // added, removed, modified
    }

    var placeholder = empty.Children[0];
    Array64<TValue> addedValues = new();
    Array64<TValue> removedValues = new();
    Array64<TValue> modifiedValues = new();
    ((Span<TValue>)addedValues).Fill(placeholder);
    ((Span<TValue>)removedValues).Fill(placeholder);
    ((Span<TValue>)modifiedValues).Fill(placeholder);

    var addedSet = new Bitset64();
    var removedSet = new Bitset64();
    var modifiedSet = new Bitset64();

    var union = ValiditySet.Union(target.ValiditySet);

    while (union.TryExtractLowestBit(out var nextUnion, out var i)) {
      union = nextUnion;

      var selfHasBit = ValiditySet.ContainsElement(i);
      var targetHasBit = target.ValiditySet.ContainsElement(i);

      switch (selfHasBit, targetHasBit) {
        case (true, true): {
          // self && target. This is a modify (if the values are different) or a no-op (if they are the same)
          if (!Object.Equals(Children[i], target.Children[i])) {
            modifiedValues[i] = target.Children[i];
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
          addedValues[i] = target.Children[i];
          addedSet = addedSet.WithElement(i);
          break;
        }

        case (false, false): {
          // can't happen
          throw new Exception("Assertion failure in CalcDifference");
        }
      }
    }
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    nodes.Add(this);
  }
}
