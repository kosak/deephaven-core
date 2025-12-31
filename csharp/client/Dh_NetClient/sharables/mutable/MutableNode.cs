//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

public sealed class MutableNode<TChild> where TChild : class {
  public static MutableNode<TChild> OfZamboni(NodeBase immutableNode) {
    immutableNode.GetChildren(childrenStorage);
    return new MutableNode<TChild>(childrenStorage, allnulls);
  }

  public TChild GetOrMakeMutableChild(int childIndex) {
    var result = MutableChildren[childIndex];
    if (result != null) {
      return result;
    }
    result = OfZamboni(ImmutableChildren[childIndex]);
    MutableChildren[childIndex] = result;
    return result;
  }

  public static readonly ImmutableNode<TChild> Empty = new();

  public ImmutableNode<TChild> GetEmptyInstanceForThisType() => Empty;

  public static ItemWithCount<ImmutableNode<TChild>> OfArray64(ReadOnlySpan<TChild> children,
    ReadOnlySpan<int> childCounts) {
    var subtreeCount = 0;
    for (var i = 0; i != children.Length; ++i) {
      subtreeCount += childCounts[i];
    }
    if (subtreeCount == 0) {
      return ItemWithCount.Of(Empty, 0);
    }
    return ItemWithCount.Of(new ImmutableNode<TChild>(children, childCounts), subtreeCount);
  }

  public Array64<TChild?> MutableChildren;
  public Array64<NodeBase<TChild>> ImmutableChildren;

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

  public ItemWithCount<MutableNode> Replace(ItemWithCount<MutableNode> self,
    int index, ItemWithCount<MutableNode> newChild) {
    Children[index] = newChild.Item;
    ChildCounts[index] = newChild.Count;

    var newChildren = new Array64<TChild>();
    var newCounts = new Array64<int>();
    ((ReadOnlySpan<TChild>)Children).CopyTo(newChildren);
    ((ReadOnlySpan<int>)ChildCounts).CopyTo(newCounts);
    newChildren[index] = newChild.Item;
    newCounts[index] = newChild.Count;
    return OfArray64(newChildren, newCounts);
  }

  public (ItemWithCount<ImmutableNode<TChild>>, ItemWithCount<ImmutableNode<TChild>>, ItemWithCount<ImmutableNode<TChild>>)
    CalcDifference(
      ItemWithCount<ImmutableNode<TChild>> self,
      ItemWithCount<ImmutableNode<TChild>> target) {
    if (!ReferenceEquals(this, self.Item)) {
      throw new Exception($"Assertion failed: this != self.Item");
    }
    var empty = ItemWithCount.Of(Empty, 0);
    if (self == target) {
      // Source and target are the same. No changes
      return (empty, empty, empty);  // added, removed, modified
    }
    if (self == empty) {
      // Relative to an empty source, everything in target was added
      return (target, empty, empty);  // added, removed, modified
    }
    if (target == empty) {
      // Relative to an empty destination, everything in src was removed
      return (empty, self, empty);  // added, removed, modified
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
      var newSelf = ItemWithCount.Of(Children[i], ChildCounts[i]);
      var newTarget = ItemWithCount.Of(target.Item.Children[i], target.Item.ChildCounts[i]);
      var (added, removed, modified) = newSelf.Item.CalcDifference(newSelf, newTarget);
      addedChildren[i] = added.Item;
      addedChildCounts[i] = added.Count;

      removedChildren[i] = removed.Item;
      removedChildCounts[i] = removed.Count;

      modifiedChildren[i] = modified.Item;
      modifiedChildCounts[i] = modified.Count;
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
