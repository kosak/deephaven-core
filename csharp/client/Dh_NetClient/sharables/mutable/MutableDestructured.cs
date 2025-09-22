//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

internal readonly struct MutableDestructured<TValue> {
  public readonly Int64 Key;
  public readonly MutableNode Depth0;
  public readonly MutableNode Depth1;
  public readonly MutableNode Depth2;
  public readonly MutableNode Depth3;
  public readonly MutableNode Depth4;
  public readonly MutableNode Depth5;
  public readonly MutableNode Depth6;
  public readonly MutableNode Depth7;
  public readonly MutableNode Depth8;
  public readonly MutableNode Depth9;
  public readonly MutableLeaf Depth10;
  public readonly int LeafIndex;

  public MutableDestructured(NodeBase depth0, Int64 key) {
    var (i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10) = Splitter.Split(key);
    Key = key;
    Depth0 = depth0.AsMutable();
    Depth1 = Depth0.Children[i0].AsMutable();
    Depth2 = Depth1.Children[i1].AsMutable();
    Depth3 = Depth2.Children[i2].AsMutable();
    Depth4 = Depth3.Children[i3].AsMutable();
    Depth5 = Depth4.Children[i4].AsMutable();
    Depth6 = Depth5.Children[i5].AsMutable();
    Depth7 = Depth6.Children[i6].AsMutable();
    Depth8 = Depth7.Children[i7].AsMutable();
    Depth9 = Depth8.Children[i8].AsMutable();
    Depth10 = Depth9.Children[i9].AsMutable();
    LeafIndex = i10;
  }

  public ItemWithCount<NodeBase> RebuildWithNewLeafHere(TValue value) {
    var (i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10) = Splitter.Split(Key);
    var newDepth10 = Depth10.With(i10, value);
    var newDepth9 = Depth9.Replace(i9, newDepth10);
    var newDepth8 = Depth8.Replace(i8, newDepth9);
    var newDepth7 = Depth7.Replace(i7, newDepth8);
    var newDepth6 = Depth6.Replace(i6, newDepth7);
    var newDepth5 = Depth5.Replace(i5, newDepth6);
    var newDepth4 = Depth4.Replace(i4, newDepth5);
    var newDepth3 = Depth3.Replace(i3, newDepth4);
    var newDepth2 = Depth2.Replace(i2, newDepth3);
    var newDepth1 = Depth1.Replace(i1, newDepth2);
    var newDepth0 = Depth0.Replace(i0, newDepth1);
    return newDepth0;
  }

  public ItemWithCount<NodeBase> RebuildWithoutLeafHere() {
    var (i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10) = Splitter.Split(Key);
    var newDepth10 = Depth10.Without(i10);
    var newDepth9 = Depth9.Replace(i9, newDepth10);
    var newDepth8 = Depth8.Replace(i8, newDepth9);
    var newDepth7 = Depth7.Replace(i7, newDepth8);
    var newDepth6 = Depth6.Replace(i6, newDepth7);
    var newDepth5 = Depth5.Replace(i5, newDepth6);
    var newDepth4 = Depth4.Replace(i4, newDepth5);
    var newDepth3 = Depth3.Replace(i3, newDepth4);
    var newDepth2 = Depth2.Replace(i2, newDepth3);
    var newDepth1 = Depth1.Replace(i1, newDepth2);
    var newDepth0 = Depth0.Replace(i0, newDepth1);
    return newDepth0;
  }
}
