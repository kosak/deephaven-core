//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

internal readonly struct Destructured<TValue> {
  public readonly Int64 Key;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>>>>>> Depth0;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>>>>> Depth1;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>>>> Depth2;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>>> Depth3;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>> Depth4;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>> Depth5;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>> Depth6;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>> Depth7;
  public readonly ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>> Depth8;
  public readonly ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>> Depth9;
  public readonly ImmutableNode<ImmutableValueHolder<TValue>> Depth10;
  public readonly int LeafIndex;

  public Destructured(ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>>>>>> depth0,
    Int64 key) {
    var (i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10) = Splitter.Split(key);
    Key = key;
    Depth0 = depth0;
    Depth1 = Depth0.Children[i0];
    Depth2 = Depth1.Children[i1];
    Depth3 = Depth2.Children[i2];
    Depth4 = Depth3.Children[i3];
    Depth5 = Depth4.Children[i4];
    Depth6 = Depth5.Children[i5];
    Depth7 = Depth6.Children[i6];
    Depth8 = Depth7.Children[i7];
    Depth9 = Depth8.Children[i8];
    Depth10 = Depth9.Children[i9];
    LeafIndex = i10;
  }

  public (ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<
    ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<
    ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>>>>>>,
    int)
    RebuildWithNewLeafHere(TValue value) {
    return RebuildWithHelper(value, 1);
  }

  public (ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<
      ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<
      ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>>>>>>,
      int) RebuildWithoutLeafHere() {
    return RebuildWithHelper(default, 0);
  }

  private (ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<
      ImmutableNode<ImmutableNode<ImmutableNode<ImmutableNode<
        ImmutableNode<ImmutableNode<ImmutableNode<ImmutableValueHolder<TValue>>>>>>>>>>>>,
    int)
    RebuildWithHelper(TValue value, int valueExists) {
    var (i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10) = Splitter.Split(Key);
    var leaf = new ImmutableValueHolder<TValue>(value);
    var newDepth10 = Depth10.Replace(i10, leaf, valueExists);
    var newDepth9 = Depth9.Replace(i9, newDepth10.Item1, newDepth10.Item2);
    var newDepth8 = Depth8.Replace(i8, newDepth9.Item1, newDepth9.Item2);
    var newDepth7 = Depth7.Replace(i7, newDepth8.Item1, newDepth8.Item2);
    var newDepth6 = Depth6.Replace(i6, newDepth7.Item1, newDepth7.Item2);
    var newDepth5 = Depth5.Replace(i5, newDepth6.Item1, newDepth6.Item2);
    var newDepth4 = Depth4.Replace(i4, newDepth5.Item1, newDepth5.Item2);
    var newDepth3 = Depth3.Replace(i3, newDepth4.Item1, newDepth4.Item2);
    var newDepth2 = Depth2.Replace(i2, newDepth3.Item1, newDepth3.Item2);
    var newDepth1 = Depth1.Replace(i1, newDepth2.Item1, newDepth2.Item2);
    var newDepth0 = Depth0.Replace(i0, newDepth1.Item1, newDepth1.Item2);
    return newDepth0;
  }

}

public static class Splitter {
  private const UInt64 SignBit = 0x8000_0000_0000_0000UL;

  private const int Shift = 6;
  private const UInt64 Mask = 0x3f;

  public static (int, int, int, int, int, int, int, int, int, int, int) Split(Int64 keySigned) {
    // After converting signed to unsigned, we still want the numbers to be ordered in the expected way.
    var key = (UInt64)keySigned ^ SignBit;
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
    temp ^= SignBit;
    return (Int64)temp;
  }
}
