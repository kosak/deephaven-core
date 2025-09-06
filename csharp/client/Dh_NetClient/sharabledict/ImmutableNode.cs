//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics.CodeAnalysis;

namespace Deephaven.Dh_NetClient;

public class ImmutableNode<T> : INode<ImmutableNode<T>> where T : INode<T> {
  public static ImmutableNode<T> OfEmpty(T emptyChild) {
    var children = new Array64<T>();
    ((Span<T>)children).Fill(emptyChild);
    return new ImmutableNode<T>(0, new Bitset64(), children);
  }

  public static ImmutableNode<T> OfArray64(ReadOnlySpan<T> children) {
    var validitySet = new Bitset64();
    var subtreeCount = 0;
    for (var i = 0; i != children.Length; ++i) {
      var child = children[i];
      validitySet = validitySet.WithElement(i);
      subtreeCount += child.Count;
    }
    return new ImmutableNode<T>(subtreeCount, validitySet, children);
  }

  public int Count { get; init; }
  public readonly Bitset64 ValiditySet;
  public readonly Array64<T> Children;

  private ImmutableNode(int count, Bitset64 validitySet, ReadOnlySpan<T> children) {
    Count = count;
    ValiditySet = validitySet;
    children.CopyTo(Children);
  }

  public ImmutableNode<T> Replace(int index, T childOrEmpty) {
    var newChildren = new Array64<T>();
    ((ReadOnlySpan<T>)Children).CopyTo(newChildren);
    newChildren[index] = childOrEmpty;
    return OfArray64(newChildren);
  }

  public ImmutableNode<T> WithLeaf(int index, T leaf) {
    var newVs = ValiditySet.WithElement(index);
    var newCount = newVs.Count;
    var newChildren = new Array64<T>();
    ((ReadOnlySpan<T>)Children).CopyTo(newChildren);
    newChildren[index] = leaf;
    return new ImmutableNode<T>(newCount, newVs, newChildren);
  }

  public ImmutableNode<T> WithoutLeaf(int index) {
    var newVs = ValiditySet.WithoutElement(index);
    var newCount = newVs.Count;
    return new ImmutableNode<T>(newCount, newVs, Children);
  }

  public bool TryGetChild(int childIndex, [MaybeNullWhen(false)] out T child) {
    if (!ValiditySet.ContainsElement(childIndex)) {
      child = default;
      return false;
    }
    child = Children[childIndex];
    return true;
  }

  public (ImmutableNode<T>, ImmutableNode<T>, ImmutableNode<T>) CalcDifference(ImmutableNode<T> target,
    ImmutableNode<T> empty) {
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

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    if (!nodes.Add(this)) {
      return;
    }
    foreach (var child in Children) {
      child.GatherNodesForUnitTesting(nodes);
    }
  }
}
