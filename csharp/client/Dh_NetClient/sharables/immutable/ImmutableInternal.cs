//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

namespace Deephaven.Dh_NetClient;

/// <summary>
/// This struct is a value type with two members: a count, and a reference to an array of children.
/// </summary>
public readonly struct ImmutableInternal<TChild> : IImmutableNode<ImmutableInternal<TChild>>
  where TChild : struct, IImmutableNode<TChild> {
  // TODO(kosak): put somewhere
  private const int NumChildren = 64;

  public static readonly ImmutableInternal<TChild> EmptyInstance = new();

  public ImmutableInternal<TChild> GetEmptyInstance() => EmptyInstance;

  public static ImmutableInternal<TChild> OfArray(TChild[] children) {
    var subtreeCount = 0;
    for (var i = 0; i != children.Length; ++i) {
      var child = children[i];
      subtreeCount += child.Count;
    }
    return subtreeCount == 0 ? EmptyInstance : new ImmutableInternal<TChild>(subtreeCount, children);
  }

  private readonly int _count;
  public readonly TChild[] Children;

  public int Count => _count;

  private ImmutableInternal(int count, TChild[] children) {
    _count = count;
    Children = children;
  }

  public ImmutableInternal<TChild> Replace(int index, TChild newChild) {
    // If we are about to replace our only non-empty child with an empty child, then canonicalize to empty.
    if (Count == Children[index].Count && newChild.Count == 0) {
      return EmptyInstance;
    }
    var newChildren = new TChild[NumChildren];
    ((ReadOnlySpan<TChild>)Children).CopyTo(newChildren);
    newChildren[index] = newChild;
    return OfArray(newChildren);
  }

  public (ImmutableInternal<TChild>, ImmutableInternal<TChild>, ImmutableInternal<TChild>) CalcDifference(
    ImmutableInternal<TChild> after) {
    var empty = EmptyInstance;
    if (ReferenceEquals(this.Children, after.Children)) {
      // Source and target are the same. No changes
      return (empty, empty, empty);  // added, removed, modified
    }
    if (this.Count == 0) {
      // Relative to an empty source, everything in target was added
      return (after, empty, empty);  // added, removed, modified
    }
    if (after.Count == 0) {
      // Relative to an empty destination, everything in src was removed
      return (empty, this, empty);  // added, removed, modified
    }

    // Need to recurse to all children to build new nodes
    var addedChildren = new TChild[NumChildren];
    var removedChildren = new TChild[NumChildren];
    var modifiedChildren = new TChild[NumChildren];

    for (var i = 0; i != NumChildren; ++i) {
      var (a, r, m) = Children[i].CalcDifference(after.Children[i]);
      addedChildren[i] = a;
      removedChildren[i] = r;
      modifiedChildren[i] = m;
    }

    var aResult = OfArray(addedChildren);
    var rResult = OfArray(removedChildren);
    var mResult = OfArray(modifiedChildren);
    return (aResult, rResult, mResult);
  }

  public void GatherNodesForUnitTesting(HashSet<object> nodes) {
    if (!nodes.Add(Children)) {
      return;
    }
    foreach (var child in Children) {
      child.GatherNodesForUnitTesting(nodes);
    }
  }
}
