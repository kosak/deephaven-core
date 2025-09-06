//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

namespace Deephaven.Dh_NetClient.Sharables.Immutable;

public class ImmutableNode<T> : ImmutableBase<ImmutableNode<T>> where T : ImmutableBase<T> {
  public static ImmutableNode<T> OfEmpty(T emptyChild) {
    var children = new Array64<T>();
    ((Span<T>)children).Fill(emptyChild);
    return new ImmutableNode<T>(0, children);
  }

  public static ImmutableNode<T> OfArray64(ReadOnlySpan<T> children) {
    var subtreeCount = 0;
    for (var i = 0; i != children.Length; ++i) {
      var child = children[i];
      subtreeCount += child.Count;
    }
    return new ImmutableNode<T>(subtreeCount, children);
  }

  public readonly Array64<T> Children;

  private ImmutableNode(int count, ReadOnlySpan<T> children) : base(count) {
    children.CopyTo(Children);
  }

  public ImmutableNode<T> Replace(int index, T childOrEmpty) {
    var newChildren = new Array64<T>();
    ((ReadOnlySpan<T>)Children).CopyTo(newChildren);
    newChildren[index] = childOrEmpty;
    return OfArray64(newChildren);
  }

  public override (ImmutableNode<T>, ImmutableNode<T>, ImmutableNode<T>) CalcDifference(
    ImmutableNode<T> target, ImmutableNode<T> empty) {
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
    Array64<T> addedChildren = new();
    Array64<T> removedChildren = new();
    Array64<T> modifiedChildren = new();

    var length = ((ReadOnlySpan<T>)Children).Length;
    for (var i = 0; i != length; ++i) {
      var (a, r, m) = Children[i].CalcDifference(target.Children[i], empty.Children[0]);
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
