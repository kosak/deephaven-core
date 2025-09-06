//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics.CodeAnalysis;

namespace Deephaven.Dh_NetClient.Sharabledict.Immutable;

public class ImmutableBase {
  public readonly int Count;

}

public class ImmutableLeaf<TValue> : INode<ImmutableNode<T>> where T : INode<T> {
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
      return (empty, empty, empty);  // added, removed, modified
    }
    if (this == empty) {
      // Relative to an empty source, everything in target was added
      return (target, empty, empty);  // added, removed, modified
    }
    if (target == empty) {
      // Relative to an empty destination, everything in src was removed
      return (empty, this, empty);  // added, removed, modified
    }

    // Need to recurse to all children to build new nodes
    Array64<TValue> addedValues = new();
    Array64<TValue> removedValues = new();
    Array64<TValue> modifiedValues = new();
    ((Span<TValue>)addedValues).Fill(placeholder);
    ((Span<TValue>)removedValues).Fill(placeholder);
    ((Span<TValue>)modifiedValues).Fill(placeholder);




    var length = ((ReadOnlySpan<TValue>)Children).Length;
    for (var i = 0; i != length; ++i) {
      var selfHasBit = ValiditySet.ContainsElement(i);
      var targetHasBit = target.ValiditySet.ContainsElement(i);

      if (selfHasBit && targetHasBit) {
        // self && target. This is a modify (if the values are different) or a no-op (if they are the same)
        if (!Object.Equals(Children[i], target.Children[i])) {
          modifiedValues[i] = target.Children[i];
          modifiedSet = modifiedSet.IncludeElement(i);
        }
        continue;
      }

      if (selfHasBit && !targetHasBit) {
        removedValues[i] = Children[i];
        removedSet = removedSet.IncludeElement(i);
        continue;

      }
      if (!selfHasBit && targetHasBit) {
        addedValues[i] = target.Children[i];
        addedSet = addedSet.IncludeElement(i);
        continue;
      }

      // if (!selfHasBit && targetHasBit)
      // do nothing
    }

    var aResult = OfArray64(addedChildren);
    var rResult = OfArray64(removedChildren);
    var mResult = OfArray64(modifiedChildren);
    return (aResult, rResult, mResult);
  }

  public (ImmutableNode<T>, ImmutableNode<T>, ImmutableNode<T>) CalcLeafDifference(
    ImmutableNode<T> target, ImmutableNode<T> empty) {
    var nubbin1 = this as ImmutableNode<ValueWrapper<double>>;
    var nubbin2 = target as ImmutableNode<ValueWrapper<double>>;
    return (null, null, null);
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    if (!nodes.Add(this)) {
      return;
    }
    foreach (var child in Children) {
      child.GatherNodesForUnitTesting(nodes);
    }
  }
}