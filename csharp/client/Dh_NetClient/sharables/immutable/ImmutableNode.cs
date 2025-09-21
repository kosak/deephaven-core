//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

namespace Deephaven.Dh_NetClient;

public sealed class ImmutableNode<TChild> : NodeBase, IAmImmutable<ImmutableNode<TChild>>
  where TChild : IAmImmutable<TChild>, new() {
  public static readonly ImmutableNode<TChild> Empty = new();

  public ImmutableNode<TChild> GetEmptyInstanceForThisType() => Empty;

  public static (ImmutableNode<TChild>, int) OfArray64(ReadOnlySpan<TChild> children,
    ReadOnlySpan<int> childCounts) {
    var subtreeCount = 0;
    for (var i = 0; i != children.Length; ++i) {
      subtreeCount += childCounts[i];
    }
    if (subtreeCount == 0) {
      return (Empty, 0);
    }
    return (new ImmutableNode<TChild>(children, childCounts), subtreeCount);
  }

  public readonly Array64<TChild> Children;
  public readonly Array64<int> ChildCounts;

  public ImmutableNode() {
    // This is our hack to access the static T.Empty for type T
    var emptyChild = new TChild().GetEmptyInstanceForThisType();
    ((Span<TChild>)Children).Fill(emptyChild);
    ((Span<int>)ChildCounts).Clear();
  }

  private ImmutableNode(ReadOnlySpan<TChild> children, ReadOnlySpan<int> childCounts) {
    children.CopyTo(Children);
    childCounts.CopyTo(ChildCounts);
  }

  public (ImmutableNode<TChild>, int) Replace(int index, TChild newChild, int newChildCount) {
    var newChildren = new Array64<TChild>();
    var newCounts = new Array64<int>();
    ((ReadOnlySpan<TChild>)Children).CopyTo(newChildren);
    ((ReadOnlySpan<int>)ChildCounts).CopyTo(newCounts);
    newChildren[index] = newChild;
    newCounts[index] = newChildCount;
    return OfArray64(newChildren, newCounts);
  }

  public (ImmutableNode<TChild>, int,
    ImmutableNode<TChild>, int,
    ImmutableNode<TChild>, int)
    CalcDifference(int thisCount, ImmutableNode<TChild> target, int targetCount) {
    var empty = Empty;
    if (this == target) {
      // Source and target are the same. No changes
      return (empty, 0, empty, 0, empty,  0);  // added, removed, modified
    }
    if (this == empty) {
      // Relative to an empty source, everything in target was added
      return (target, targetCount, empty, 0, empty, 0);  // added, removed, modified
    }
    if (target == empty) {
      // Relative to an empty destination, everything in src was removed
      return (empty, 0, this, thisCount, empty, 0);  // added, removed, modified
    }

    // Need to recurse to all children to build new nodes
    Array64<TChild> addedChildren = new();
    Array64<TChild> removedChildren = new();
    Array64<TChild> modifiedChildren = new();
    Array64<int> addedChildCounts = new();
    Array64<int> removedChildCounts = new();
    Array64<int> modifiedChildCounts = new();

    var length = ((ReadOnlySpan<TChild>)Children).Length;
    for (var i = 0; i != length; ++i) {
      var (ach, acnt, rch, rcnt, mch, mcnt) = Children[i].CalcDifference(
        ChildCounts[i], target.Children[i], target.ChildCounts[i]);
      addedChildren[i] = ach;
      addedChildCounts[i] = acnt;

      removedChildren[i] = rch;
      removedChildCounts[i] = rcnt;

      modifiedChildren[i] = mch;
      modifiedChildCounts[i] = mcnt;
    }

    var (aChildren, aCount) = OfArray64(addedChildren, addedChildCounts);
    var (rChildren, rCount) = OfArray64(removedChildren, removedChildCounts);
    var (mChildren, mCount) = OfArray64(modifiedChildren, modifiedChildCounts);
    return (aChildren, aCount, rChildren, rCount, mChildren, mCount);
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
