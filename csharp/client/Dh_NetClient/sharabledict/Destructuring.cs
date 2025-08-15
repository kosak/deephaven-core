//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

internal readonly struct Destructured<TValue> {
  public readonly Int64 Key;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>>>>>>>>> Root;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>>>>>>>> Child0;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>>>>>>> Child1;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>>>>>> Child2;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>>>>> Child3;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>>>> Child4;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>>> Child5;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>> Child6;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>> Child7;
  public readonly ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>> Child8;
  public readonly ImmutableNode<ZamboniWrap<TValue>> Leaf;
  public readonly int LeafIndex;

  public Destructured(ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ZamboniWrap<TValue>>>>>>>>>>>> root,
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

  public SharableDict<TValue> RebuildWith(ImmutableNode<ZamboniWrap<TValue>> newLeaf) {
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
