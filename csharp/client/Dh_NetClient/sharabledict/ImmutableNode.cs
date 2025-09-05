//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics.CodeAnalysis;

namespace Deephaven.Dh_NetClient;

public class ImmutableNode<T> : INode<ImmutableNode<T>> where T : INode<T> {
  public static ImmutableNode<T> EmptyInstance { get; } = new();

  public static ImmutableNode<T> OfArray64(ReadOnlySpan<T> children) {
    var validitySet = new Bitset64();
    var count = 0;
    for (var i = 0; i != children.Length; ++i) {
      var child = children[i];
      if (child.IsEmpty) {
        continue;
      }
      validitySet = validitySet.WithElement(i);
      count += child.Count;
    }
    return Create(validitySet, count, children, 0, children[0]);
  }

  public static ImmutableNode<T> Create(Bitset64 validitySet, int subtreeCount,
    ReadOnlySpan<T> children, int replacementIndex, T replacementChild) {
    if (validitySet.IsEmpty) {
      return EmptyInstance;
    }
    return new ImmutableNode<T>(validitySet, subtreeCount, children, replacementIndex,
      replacementChild);
  }

  public int Count { get; init; }
  public readonly Bitset64 ValiditySet;
  public readonly Array64<T> Children;

  private ImmutableNode() {
    ((Span<T>)Children).Fill(T.EmptyInstance);
  }

  private ImmutableNode(Bitset64 validitySet, int count, ReadOnlySpan<T> children,
    int replacementIndex, T replacementChild) {
    Count = count;
    ValiditySet = validitySet;
    children.CopyTo(Children);
    Children[replacementIndex] = replacementChild;
  }

  public ImmutableNode<T> With(int index, T child) {
    if (child.IsEmpty) {
      return Without(index);
    }
    var newVs = ValiditySet.WithElement(index);
    var newCount = Count - Children[index].Count + child.Count;
    return Create(newVs, newCount, Children, index, child);
  }

  public ImmutableNode<T> Without(int index) {
    var newVs = ValiditySet.WithoutElement(index);
    var newCount = Count - Children[index].Count;
    return Create(newVs, newCount, Children, index, T.EmptyInstance);
  }

  public bool TryGetChild(int childIndex, [MaybeNullWhen(false)] out T child) {
    if (!ValiditySet.ContainsElement(childIndex)) {
      child = default;
      return false;
    }
    child = Children[childIndex];
    return true;
  }

  public (ImmutableNode<T>, ImmutableNode<T>, ImmutableNode<T>) CalcDifference(ImmutableNode<T> target) {
    if (this == target) {
      // Source and target are the same. No changes
      return (EmptyInstance, EmptyInstance, EmptyInstance);  // added, removed, modified
    }
    if (this == EmptyInstance) {
      // Relative to an empty source, everything in target was added
      return (target, EmptyInstance, EmptyInstance);  // added, removed, modified
    }
    if (target == EmptyInstance) {
      // Relative to an empty destination, everything in src was removed
      return (EmptyInstance, this, EmptyInstance);  // added, removed, modified
    }
    // Need to recurse to all children to build new nodes
    Array64<T> addedChildren = new();
    Array64<T> removedChildren = new();
    Array64<T> modifiedChildren = new();

    var length = ((ReadOnlySpan<T>)Children).Length;
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

  public bool IsEmpty => ValiditySet.IsEmpty;
}
