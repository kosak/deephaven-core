//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

using System.Collections;
using System.Diagnostics.CodeAnalysis;

public class SharableDict<TValue> : IReadOnlyDictionary<Int64, TValue> {
  public static readonly SharableDict<TValue> Empty = new();

  private readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ValueWrapper<TValue>>>>>>>>>>>> _root;

  public SharableDict() {
    _root = ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ValueWrapper<TValue>>>>>>>>>>>>.EmptyInstance;
  }

  public SharableDict(
    ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ValueWrapper<TValue>>>>>>>>>>>>
      root) {
    _root = root;
  }

  public SharableDict<TValue> With(Int64 key, TValue value) {
    return new Destructured<TValue>(_root, key).RebuildWithNewLeaf(value);
  }

  public SharableDict<TValue> Without(Int64 key) {
    return new Destructured<TValue>(_root, key).RebuildWithoutLeaf();
  }

  public bool TryGetValue(Int64 key, [MaybeNullWhen(false)] out TValue value) {
    var s = new Destructured<TValue>(_root, key);
    if (!s.Depth10.TryGetChild(s.LeafIndex, out var wrappedValue)) {
      value = default;
      return false;
    }
    value = wrappedValue.Value;
    return true;
  }

  public bool ContainsKey(Int64 key) {
    return TryGetValue(key, out _);
  }

  public (SharableDict<TValue>, SharableDict<TValue>, SharableDict<TValue>)
    CalcDifference(SharableDict<TValue> target) {
    var (added, removed, modified) = _root.CalcDifference(target._root);
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
      var depth1 = _root.Children[i0];
      foreach (var i1 in depth1.ValiditySet) {
        var depth2 = depth1.Children[i1];
        foreach (var i2 in depth2.ValiditySet) {
          var depth3 = depth2.Children[i2];
          foreach (var i3 in depth3.ValiditySet) {
            var depth4 = depth3.Children[i3];
            foreach (var i4 in depth4.ValiditySet) {
              var depth5 = depth4.Children[i4];
              foreach (var i5 in depth5.ValiditySet) {
                var depth6 = depth5.Children[i5];
                foreach (var i6 in depth6.ValiditySet) {
                  var depth7 = depth6.Children[i6];
                  foreach (var i7 in depth7.ValiditySet) {
                    var depth8 = depth7.Children[i7];
                    foreach (var i8 in depth8.ValiditySet) {
                      var depth9 = depth8.Children[i8];
                      foreach (var i9 in depth9.ValiditySet) {
                        var depth10 = depth9.Children[i9];
                        foreach (var i10 in depth10.ValiditySet) {
                          var value = depth10.Children[i10].Value;
                          var offset = Splitter.Merge(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10);
                          yield return KeyValuePair.Create(offset, value);
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

  public override string ToString() {
    return string.Join(", ", this.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
  }
}
