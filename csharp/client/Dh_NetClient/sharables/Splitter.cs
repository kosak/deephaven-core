//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

internal static class Splitter {
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
