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

  public ((ImmutableNode<TChild>, int),
    (ImmutableNode<TChild>, int),
    (ImmutableNode<TChild>, int))
    CalcDifference(
      (ImmutableNode<TChild>, int) self,
      (ImmutableNode<TChild>, int) target) {
    if (!ReferenceEquals(this, self.Item1)) {
      throw new Exception($"Assertion failed: this != self.Item1");
    }
    var empty0 = (Empty, 0);
    if (self == target) {
      // Source and target are the same. No changes
      return (empty0, empty0, empty0);  // added, removed, modified
    }
    if (self == empty0) {
      // Relative to an empty source, everything in target was added
      return (target, empty0, empty0);  // added, removed, modified
    }
    if (target == empty0) {
      // Relative to an empty destination, everything in src was removed
      return (empty0, self, empty0);  // added, removed, modified
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
      var newSelf = (Children[i], ChildCounts[i]);
      var newTarget = (target.Item1.Children[i], target.Item1.ChildCounts[i]);
      var (added, removed, modified) = newSelf.Item1.CalcDifference(newSelf, newTarget);
      addedChildren[i] = added.Item1;
      addedChildCounts[i] = added.Item2;

      removedChildren[i] = removed.Item1;
      removedChildCounts[i] = removed.Item2;

      modifiedChildren[i] = modified.Item1;
      modifiedChildCounts[i] = modified.Item2;
    }

    var aResult = OfArray64(addedChildren, addedChildCounts);
    var rResult = OfArray64(removedChildren, removedChildCounts);
    var mResult = OfArray64(modifiedChildren, modifiedChildCounts);
    return (aResult, rResult, mResult);
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
