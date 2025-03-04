using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public readonly struct SharableDict<TValue> : IReadOnlyDictionary<Int64, TValue> {
  private readonly Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>>>>>> _root;

  public SharableDict() {
    _root = Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>>>>>>.Empty;
  }

  public SharableDict(
    Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>>>>>>
      root) {
    _root = root;
  }

  public SharableDict<TValue> With(Int64 key, TValue value) {
    var s = new Destructured<TValue>(_root, key);
    var newLeaf = s.Leaf.With(s.LeafIndex, value);
    return s.RebuildWith(newLeaf);
  }

  public SharableDict<TValue> Without(Int64 key) {
    var s = new Destructured<TValue>(_root, key);
    var newLeaf = s.Leaf.Without(s.LeafIndex);
    return s.RebuildWith(newLeaf);
  }

  public bool TryGetValue(Int64 key, [MaybeNullWhen(false)] out TValue value) {
    var s = new Destructured<TValue>(_root, key);
    return s.Leaf.TryGetValue(s.LeafIndex, out value);
  }

  public bool ContainsKey(Int64 key) {
    return TryGetValue(key, out _);
  }

  public (SharableDict<TValue>, SharableDict<TValue>, SharableDict<TValue>)
    CalcDifference(SharableDict<TValue> other) {
    var (added, removed, modified) = _root.CalcDifference(other._root);
    var aResult = new SharableDict<TValue>(added);
    var rResult = new SharableDict<TValue>(removed);
    var mResult = new SharableDict<TValue>(modified);
    return (aResult, rResult, mResult);
  }

  public TValue this[Int64 key] {
    get {
      if (!TryGetValue(key, out var value)) {
        throw new KeyNotFoundException();
      }

      return value;
    }
  }

  public IEnumerable<Int64> Keys => this.Select(kvp => kvp.Key);
  public IEnumerable<TValue> Values => this.Select(kvp => kvp.Value);

  public IEnumerator<KeyValuePair<Int64, TValue>> GetEnumerator() {
    // This could be written more nicely and recursively as a bunch of nested iterators,
    // but the overhead of fetching each element would be pretty high, as each iterator
    // would call the MoveNext of the next iterator, etc.
    // Manually unrolling the structure into these nested foreach is a little bit homely
    // but allows for more efficient code.
    foreach (var i0 in _root.ValiditySet) {
      var child0 = _root.Children[i0];
      foreach (var i1 in child0.ValiditySet) {
        var child1 = child0.Children[i1];
        foreach (var i2 in child1.ValiditySet) {
          var child2 = child1.Children[i2];
          foreach (var i3 in child2.ValiditySet) {
            var child3 = child2.Children[i3];
            foreach (var i4 in child3.ValiditySet) {
              var child4 = child3.Children[i4];
              foreach (var i5 in child4.ValiditySet) {
                var child5 = child4.Children[i5];
                foreach (var i6 in child5.ValiditySet) {
                  var child6 = child5.Children[i6];
                  foreach (var i7 in child6.ValiditySet) {
                    var child7 = child6.Children[i7];
                    foreach (var i8 in child7.ValiditySet) {
                      var child8 = child7.Children[i8];
                      foreach (var i9 in child8.ValiditySet) {
                        var child9 = child8.Children[i9];
                        foreach (var i10 in child9.ValiditySet) {
                          var data = child9.Data[i10];
                          var offset = Splitter.Merge(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10);
                          yield return KeyValuePair.Create(offset, data);
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public int Count => _root.Count;
}

internal readonly struct Destructured<TValue> {
  public readonly Int64 Key;
  public readonly Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>>>>>> Root;
  public readonly Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>>>>> Child0;
  public readonly Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>>>> Child1;
  public readonly Internal<Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>>> Child2;
  public readonly Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>> Child3;
  public readonly Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>> Child4;
  public readonly Internal<Internal<Internal<Internal<Leaf<TValue>>>>> Child5;
  public readonly Internal<Internal<Internal<Leaf<TValue>>>> Child6;
  public readonly Internal<Internal<Leaf<TValue>>> Child7;
  public readonly Internal<Leaf<TValue>> Child8;
  public readonly Leaf<TValue> Leaf;
  public readonly int LeafIndex;

  public Destructured(Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Internal<Leaf<TValue>>>>>>>>>>> root,
    Int64 key) {
    Key = key;
    Root = root;
    var (i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10) = Splitter.Split(key);
    Child0 = Root.Children[i0];
    Child1 = Child0.Children[i1];
    Child2 = Child1.Children[i2];
    Child3 = Child2.Children[i3];
    Child4 = Child3.Children[i4];
    Child5 = Child4.Children[i5];
    Child6 = Child5.Children[i6];
    Child7 = Child6.Children[i7];
    Child8 = Child7.Children[i8];
    Leaf = Child8.Children[i9];
    LeafIndex = i10;
  }

  public SharableDict<TValue> RebuildWith(Leaf<TValue> newLeaf) {
    var (i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, _) = Splitter.Split(Key);
    var newChild8 = Child8.With(i9, newLeaf);
    var newChild7 = Child7.With(i8, newChild8);
    var newChild6 = Child6.With(i7, newChild7);
    var newChild5 = Child5.With(i6, newChild6);
    var newChild4 = Child4.With(i5, newChild5);
    var newChild3 = Child3.With(i4, newChild4);
    var newChild2 = Child2.With(i3, newChild3);
    var newChild1 = Child1.With(i2, newChild2);
    var newChild0 = Child0.With(i1, newChild1);
    var newRoot = Root.With(i0, newChild0);
    return new SharableDict<TValue>(newRoot);
  }
}

public static class Splitter {
  private const int Shift = 6;
  private const UInt64 Mask = 0x3f;

  public static (int, int, int, int, int, int, int, int, int, int, int) Split(Int64 keySigned) {
    var key = (UInt64)keySigned;
    var i10 = (int)(key & Mask);
    key >>= Shift;
    var i9 = (int)(key & Mask);
    key >>= Shift;
    var i8 = (int)(key & Mask);
    key >>= Shift;
    var i7 = (int)(key & Mask);
    key >>= Shift;
    var i6 = (int)(key & Mask);
    key >>= Shift;
    var i5 = (int)(key & Mask);
    key >>= Shift;
    var i4 = (int)(key & Mask);
    key >>= Shift;
    var i3 = (int)(key & Mask);
    key >>= Shift;
    var i2 = (int)(key & Mask);
    key >>= Shift;
    var i1 = (int)(key & Mask);
    key >>= Shift;
    var i0 = (int)(key & Mask);
    return (i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10);
  }

  public static Int64 Merge(int i0, int i1, int i2, int i3,
    int i4, int i5, int i6, int i7, int i8, int i9, int i10) {
    var temp = (UInt64)i0;
    temp = (temp << Shift) | (uint)i1;
    temp = (temp << Shift) | (uint)i2;
    temp = (temp << Shift) | (uint)i3;
    temp = (temp << Shift) | (uint)i4;
    temp = (temp << Shift) | (uint)i5;
    temp = (temp << Shift) | (uint)i6;
    temp = (temp << Shift) | (uint)i7;
    temp = (temp << Shift) | (uint)i8;
    temp = (temp << Shift) | (uint)i9;
    temp = (temp << Shift) | (uint)i10;
    return (Int64)temp;
  }
}

public interface INode<out T> {
  public static abstract T Empty { get; }
}

public abstract class NodeBase {
  public readonly int Count = 0;
  public readonly Bitset64 ValiditySet;

  protected NodeBase() {
    // defaults
  }

  protected NodeBase(int count, Bitset64 validitySet) {
    Count = count;
    ValiditySet = validitySet;
  }
}

public class Internal<T> : NodeBase, INode<Internal<T>> where T : NodeBase, INode<T> {
  public static Internal<T> Empty { get; } = new();

  public readonly Array64<T?> Children;

  private Internal() {
    ((Span<T?>)Children).Fill(T.Empty);
  }

  private Internal(int count, Bitset64 validitySet, ReadOnlySpan<T?> children,
    int replacementIndex, T? replacementChild) : base(count, validitySet) {
    children.CopyTo(Children);
    Children[replacementIndex] = replacementChild;
  }

  private static (int, Bitset64) CalcCountAndValiditySet(Internal<T> other,
    int newIndex, T newChild) {
    var adding = newChild != T.Empty;
    var otherVs = other.ValiditySet;
    var newVs = adding ? otherVs.WithElement(newIndex) :
      otherVs.WithoutElement(newIndex);
    var newCount = other.Count - other.Children[newIndex].Count + newChild.Count;
    return (newCount, newVs);
  }

  public Internal<T> With(int index, T child) {
    // Convenience because we sometimes call "With" with an empty child
    if (child == T.Empty) {
      return Without(index);
    }
    var newVs = ValiditySet.WithElement(index);
    var newCount = Count - Children[index].Count + child.Count;
    return new Internal<T>(newCount, newVs, Children, index, child);
  }

  private Internal<T> Without(int index) {
    if (ValiditySet.WithoutElement(index).IsEmpty) {
      return Empty;
    }
    return new Internal<T>(this, index, T.Empty);
  }

  
}

public class Leaf<T> : NodeBase, INode<Leaf<T>> {
  public static Leaf<T> Empty { get; } = new();

  public readonly Array64<T?> Data;

  private Leaf() {
  }

  private Leaf(int count, Bitset64 validitySet, ReadOnlySpan<T?> data,
    int replacementIndex, T? replacementData) : base(count, validitySet) {
    data.CopyTo(Data);
    Data[replacementIndex] = replacementData;
  }

  public bool TryGetValue(int index, out T? value) {
    if (!ValiditySet.ContainsElement(index)) {
      value = default;
      return false;
    }

    value = Data[index];
    return true;
  }

  public Leaf<T> With(int index, T value) {
    var newVs = ValiditySet.WithElement(index);
    var subtreeSize = newVs.Count;
    return new Leaf<T>(subtreeSize, newVs, Data, index, value);
  }

  public Leaf<T> Without(int index) {
    var newVs = ValiditySet.WithoutElement(index);
    if (newVs.IsEmpty) {
      return Empty;
    }
    var subtreeSize = newVs.Count;
    return new Leaf<T>(subtreeSize, newVs, Data, index, default);
  }
}

public readonly struct Bitset64(UInt64 value) : IEquatable<Bitset64> {
  private readonly UInt64 _value = value;

  public Bitset64 WithElement(int element) {
    return new Bitset64(_value | ((UInt64)1 << element));
  }

  public Bitset64 WithoutElement(int element) {
    return new Bitset64(_value & ~((UInt64)1 << element));
  }

  public bool TryExtractLowestBit(out Bitset64 result, out int element) {
    if (IsEmpty) {
      result = default;
      element = default;
      return false;
    }

    element = System.Numerics.BitOperations.TrailingZeroCount(_value);
    result = WithoutElement(element);
    return true;
  }

  public int Count => System.Numerics.BitOperations.PopCount(_value);

  public bool ContainsElement(int element) {
    return (_value & ((UInt64)1 << element)) != 0;
  }

  public bool Equals(Bitset64 other) {
    return _value == other._value;
  }

  public bool IsEmpty => _value == 0;

  public Enumerator GetEnumerator() {
    return new Enumerator(this);
  }

  public struct Enumerator(Bitset64 bitset) {
    private int _current;

    public bool MoveNext() {
      if (!bitset.TryExtractLowestBit(out var newBitset, out _current)) {
        return false;
      }
      bitset = newBitset;
      return true;
    }

    public int Current => _current;
  }
}

[InlineArray(64)]
public struct Array64<T> {
  public T Item;
}
