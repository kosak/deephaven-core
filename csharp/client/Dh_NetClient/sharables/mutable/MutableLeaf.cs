//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics.CodeAnalysis;

namespace Deephaven.Dh_NetClient;

public sealed class MutableLeaf<TValue> : NodeBase {
  public static readonly ImmutableLeaf<TValue> Empty = new();

  public ImmutableLeaf<TValue> GetEmptyInstanceForThisType() => Empty;

  public static ImmutableLeaf<TValue> Of(Bitset64 validitySet, ReadOnlySpan<TValue> children) {
    return validitySet.IsEmpty ? Empty : new ImmutableLeaf<TValue>(validitySet, children);
  }

  public Bitset64 ValiditySet;
  public Array64<TValue> Children;

  /// <summary>
  /// This constructor is used only to make the Empty singleton. No one else should call it.
  /// We are keeping it public because our generics have a new() constraint on them, which
  /// requires that this be public.
  /// </summary>
  public ImmutableLeaf() {
  }

  private ImmutableLeaf(Bitset64 validitySet, ReadOnlySpan<TValue> children) {
    ValiditySet = validitySet;
    children.CopyTo(Children);
  }

  public ItemWithCount<ImmutableLeaf<TValue>> With(int index, TValue value) {
    var newVs = ValiditySet.WithElement(index);
    var newCount = newVs.Count;
    var newChildren = new Array64<TValue>();
    ((ReadOnlySpan<TValue>)Children).CopyTo(newChildren);
    newChildren[index] = value;
    var newLeaf = Of(newVs, newChildren);
    return ItemWithCount.Of(newLeaf, newCount);
  }

  public ItemWithCount<ImmutableLeaf<TValue>> Without(int index) {
    var newVs = ValiditySet.WithoutElement(index);
    if (newVs.IsEmpty) {
      return ItemWithCount.Of(Empty, 0);
    }
    var newCount = newVs.Count;
    var newChildren = new Array64<TValue>();
    ((ReadOnlySpan<TValue>)Children).CopyTo(newChildren);
    newChildren[index] = default;
    var newLeaf = Of(newVs, newChildren);
    return ItemWithCount.Of(newLeaf, newCount);
  }

  public bool TryGetValue(int childIndex, [MaybeNullWhen(false)] out TValue value) {
    if (!ValiditySet.ContainsElement(childIndex)) {
      value = default;
      return false;
    }
    value = Children[childIndex];
    return true;
  }

  public (ItemWithCount<ImmutableLeaf<TValue>>,
    ItemWithCount<ImmutableLeaf<TValue>>,
    ItemWithCount<ImmutableLeaf<TValue>>) CalcDifference(ItemWithCount<ImmutableLeaf<TValue>> self,
      ItemWithCount<ImmutableLeaf<TValue>> target) {
    if (!ReferenceEquals(this, self.Item)) {
      throw new Exception($"Assertion failed: this != self.Item");
    }
    var empty = ItemWithCount.Of(Empty, 0);
    if (self == target) {
      // Source and target are the same. No changes
      return (empty, empty, empty); // added, removed, modified
    }
    if (self == empty) {
      // Relative to an empty source, everything in target was added
      return (target, empty, empty); // added, removed, modified
    }
    if (target == empty) {
      // Relative to an empty destination, everything in src was removed
      return (empty, self, empty); // added, removed, modified
    }

    Array64<TValue> addedValues = new();
    Array64<TValue> removedValues = new();
    Array64<TValue> modifiedValues = new();

    var addedSet = new Bitset64();
    var removedSet = new Bitset64();
    var modifiedSet = new Bitset64();

    var targetItem = target.Item;
    var union = ValiditySet.Union(targetItem.ValiditySet);

    while (union.TryExtractLowestBit(out var nextUnion, out var i)) {
      union = nextUnion;

      var selfHasBit = ValiditySet.ContainsElement(i);
      var targetHasBit = targetItem.ValiditySet.ContainsElement(i);

      switch (selfHasBit, targetHasBit) {
        case (true, true): {
            // self && target. This is a modify (if the values are different) or a no-op (if they are the same)
            if (!Object.Equals(Children[i], targetItem.Children[i])) {
              modifiedValues[i] = targetItem.Children[i];
              modifiedSet = modifiedSet.WithElement(i);
            }
            break;
          }

        case (true, false): {
            // self but not target
            removedValues[i] = Children[i];
            removedSet = removedSet.WithElement(i);
            break;
          }

        case (false, true): {
            // target but not self
            addedValues[i] = targetItem.Children[i];
            addedSet = addedSet.WithElement(i);
            break;
          }

        case (false, false): {
            // can't happen
            throw new Exception("Assertion failure: (false, false) in CalcDifference");
          }
      }
    }

    var aLeaf = Of(addedSet, addedValues);
    var rLeaf = Of(removedSet, removedValues);
    var mLeaf = Of(modifiedSet, modifiedValues);

    var aResult = ItemWithCount.Of(aLeaf, addedSet.Count);
    var rResult = ItemWithCount.Of(rLeaf, removedSet.Count);
    var mResult = ItemWithCount.Of(mLeaf, modifiedSet.Count);

    return (aResult, rResult, mResult);
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    nodes.Add(this);
  }
}
