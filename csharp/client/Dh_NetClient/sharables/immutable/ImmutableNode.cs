//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

namespace Deephaven.Dh_NetClient;

public sealed class ImmutableNode<TChild> : NodeBase, IAmImmutable where TChild : IAmImmutable, new() {
  public static readonly ImmutableNode<TChild> Empty = new();

  public override ImmutableNode<TChild> GetEmptyInstanceForThisType() => Empty;

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

  public override (ImmutableNode<TChild>, ImmutableNode<TChild>, ImmutableNode<TChild>) CalcDifference(
    ImmutableNode<TChild> target) {
    var empty = Empty;
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
    Array64<TChild> addedChildren = new();
    Array64<TChild> removedChildren = new();
    Array64<TChild> modifiedChildren = new();

    var length = ((ReadOnlySpan<TChild>)Children).Length;
    for (var i = 0; i != length; ++i) {
      var (a, r, m) = Children[i].CalcDifference(target.Children[i]);
      addedChildren[i] = a;
      removedChildren[i] = r;
      modifiedChildren[i] = m;
    }

    var aResult = OfArray64(addedChildren);
    var rResult = OfArray64(removedChildren);
    var mResult = OfArray64(modifiedChildren);
    return (aResult, rResult, mResult);
  }

  public override void GatherNodesForUnitTesting(HashSet<object> nodes) {
    if (!nodes.Add(this)) {
      return;
    }
    foreach (var child in Children) {
      child.GatherNodesForUnitTesting(nodes);
    }
  }
}
