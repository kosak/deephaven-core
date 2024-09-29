using Apache.Arrow;

namespace Deephaven.ManagedClient;

public class CharArrowColumnSource : ICharColumnSource {
  public static CharArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<UInt16Array>(chunkedArray);
    return new CharArrowColumnSource(arrays);
  }

  private readonly UInt16Array[] _arrays;

  private CharArrowColumnSource(UInt16Array[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, Chunk<bool>? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, v => (char)v);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class ByteArrowColumnSource : IByteColumnSource {
  public static ByteArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<Int8Array>(chunkedArray);
    return new ByteArrowColumnSource(arrays);
  }

  private readonly Int8Array[] _arrays;

  private ByteArrowColumnSource(Int8Array[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, Chunk<bool>? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, v => v);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int16ArrowColumnSource(ChunkedArray chunkedArray) : IInt16ColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var typedDest = (Int16Chunk)destData;
    var pac = new PrimitiveArrayCopier<Int16>(typedDest, nullFlags);
    Zamboni2Helpers.FillChunk(rows, chunkedArray, pac.DoCopy);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int32ArrowColumnSource(ChunkedArray chunkedArray) : IInt32ColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var typedDest = (Int32Chunk)destData;
    var pac = new PrimitiveArrayCopier<Int32>(typedDest, nullFlags);
    Zamboni2Helpers.FillChunk(rows, chunkedArray, pac.DoCopy);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int64ArrowColumnSource(ChunkedArray chunkedArray) : IInt64ColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var typedDest = (Int64Chunk)destData;
    var pac = new PrimitiveArrayCopier<Int64>(typedDest, nullFlags);
    Zamboni2Helpers.FillChunk(rows, chunkedArray, pac.DoCopy);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class FloatArrowColumnSource : IFloatColumnSource {
  public static FloatArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<FloatArray>(chunkedArray);
    return new FloatArrowColumnSource(arrays);
  }

  private readonly FloatArray[] _arrays;

  private FloatArrowColumnSource(FloatArray[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, v => v);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class DoubleArrowColumnSource : IDoubleColumnSource {
  public static DoubleArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<DoubleArray>(chunkedArray);
    return new DoubleArrowColumnSource(arrays);
  }

  private readonly DoubleArray[] _arrays;

  private DoubleArrowColumnSource(DoubleArray[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, v => v);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class BooleanArrowColumnSource(ChunkedArray chunkedArray) : IBooleanColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var typedDest = (BooleanChunk)destData;
    void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
      var typedSrc = (BooleanArray)src;
      for (var i = 0; i < count; ++i) {
        var value = typedSrc.GetValue(srcOffset);
        var valueToUse = value ?? false;
        var isNullToUse = !value.HasValue;

        typedDest.Data[destOffset] = valueToUse;
        if (nullFlags != null) {
          nullFlags.Data[destOffset] = isNullToUse;
        }

        ++srcOffset;
        ++destOffset;
      }
    }
    Zamboni2Helpers.FillChunk(rows, chunkedArray, DoCopy);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class StringArrowColumnSource(ChunkedArray chunkedArray) : IStringColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var typedDest = (StringChunk)destData;
    void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
      var typedSrc = (StringArray)src;
      for (var i = 0; i < count; ++i) {
        var value = typedSrc.GetString(srcOffset);

        typedDest.Data[destOffset] = value;
        if (nullFlags != null) {
          nullFlags.Data[destOffset] = value == null;
        }

        ++srcOffset;
        ++destOffset;
      }
    }
    Zamboni2Helpers.FillChunk(rows, chunkedArray, DoCopy);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}


public class TimestampArrowColumnSource : ITimestampColumnSource {
  public static TimestampArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<TimestampArray>(chunkedArray);
    return new TimestampArrowColumnSource(arrays);
  }

  private readonly TimestampArray[] _arrays;

  private TimestampArrowColumnSource(TimestampArray[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, DhDateTime.FromNanos);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class LocalDateArrowColumnSource : ILocalDateColumnSource {
  public static LocalDateArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<Date64Array>(chunkedArray);
    return new LocalDateArrowColumnSource(arrays);
  }

  private readonly Date64Array[] _arrays;

  private LocalDateArrowColumnSource(Date64Array[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, LocalDate.FromMillis);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class LocalTimeArrowColumnSource : ILocalTimeColumnSource {
  public static LocalTimeArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<Time64Array>(chunkedArray);
    return new LocalTimeArrowColumnSource(arrays);
  }

  private readonly Time64Array[] _arrays;

  private LocalTimeArrowColumnSource(Time64Array[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, LocalTime.FromNanos);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

class PrimitiveArrayCopier<T>(Chunk<T> typedDest, BooleanChunk? nullFlags) where T : struct, IEquatable<T> {
  public void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
    var typedSrc = (PrimitiveArray<T>)src;
    for (var i = 0; i < count; ++i) {
      var value = typedSrc.GetValue(srcOffset);
      var valueToUse = value ?? default;
      var isNullToUse = !value.HasValue;

      typedDest.Data[destOffset] = valueToUse;
      if (nullFlags != null) {
        nullFlags.Data[destOffset] = isNullToUse;
      }

      ++srcOffset;
      ++destOffset;
    }
  }
}

public static class ZamboniHelpers {
  public static TArray[] CastChunkedArray<TArray>(ChunkedArray chunkedArray) {
    var arrays = new TArray[chunkedArray.ArrayCount];
    for (var i = 0; i < chunkedArray.ArrayCount; i++) {
      arrays[i] = (TArray)chunkedArray.ArrowArray(i);
    }
    return arrays;
  }

  public static void FillChunk<TArrowElement, TChunkElement>(RowSequence rows,
    IReadOnlyList<PrimitiveArray<TArrowElement>> srcArrays,
    Chunk destData, BooleanChunk? nullFlags,
    Func<TArrowElement, TChunkElement> converter) where TArrowElement : struct, IEquatable<TArrowElement> {
    if (rows.Empty) {
      return;
    }

    // This algorithm is a little tricky because the source data and RowSequence are both
    // segmented, perhaps in different ways.
    var typedDest = (Chunk<TChunkElement>)destData;
    var destSpan = new Span<TChunkElement>(typedDest.Data);

    var srcIterator = new ZamboniIterator<TArrowElement>(srcArrays);
    var nullSpan = nullFlags != null ? new Span<bool>(nullFlags.Data) : null;

    foreach (var (reqBeginConst, reqEnd) in rows.Intervals) {
      var reqBegin = reqBeginConst;
      while (true) {
        var reqLength = (reqEnd - reqBegin).ToIntExact();
        if (reqLength == 0) {
          return;
        }

        srcIterator.Advance(reqBegin);
        var actualLength = srcIterator.CopyTo(destSpan, nullSpan, reqLength, converter);
        destSpan = destSpan.Slice(actualLength);

        if (nullSpan != null) {
          nullSpan = nullSpan.Slice(actualLength);
        }

        reqBegin += actualLength;
      }
    }
  }
}

public static class Zamboni2Helpers {
  public static void FillChunk(RowSequence rows, ChunkedArray srcArray,
    Action<IArrowArray, int, int, int> doCopy) {
    if (rows.Empty) {
      return;
    }

    var srcIterator = new Zamboni2Iterator(srcArray);
    var destIndex = 0;

    foreach (var (reqBeginConst, reqEnd) in rows.Intervals) {
      var reqBegin = reqBeginConst;
      while (true) {
        var reqLength = (reqEnd - reqBegin).ToIntExact();
        if (reqLength == 0) {
          return;
        }

        srcIterator.Advance(reqBegin);
        var amountToCopy = Math.Min(reqLength, srcIterator.SegmentLength);
        doCopy(srcIterator.CurrentSegment, srcIterator.RelativeBegin, destIndex, amountToCopy);

        reqBegin += amountToCopy;
        destIndex += amountToCopy;
      }
    }
  }
}

public class Zamboni2Iterator {
  private readonly ChunkedArray _chunkedArray;
  private int _arrayIndex = -1;
  private Int64 _segmentOffset = 0;
  private Int64 _segmentBegin = 0;
  private Int64 _segmentEnd = 0;


  public Zamboni2Iterator(ChunkedArray chunkedArray) {
    _chunkedArray = chunkedArray;
  }

  public void Advance(Int64 start) {
    while (true) {
      if (start < _segmentBegin) {
        throw new Exception($"Programming error: Can't go backwards from {_segmentBegin} to {start}");
      }

      if (start < _segmentEnd) {
        // satisfiable with current segment
        _segmentBegin = start;
        return;
      }

      // Go to next array slice (or the first one, if this is the first call to Advance)
      ++_arrayIndex;
      if (_arrayIndex >= _chunkedArray.ArrayCount) {
        throw new Exception($"Ran out of src data before processing all of RowSequence");
      }

      _segmentBegin = _segmentEnd;
      _segmentEnd = _segmentBegin + _chunkedArray.ArrowArray(_arrayIndex).Length;
      _segmentOffset = _segmentBegin;
    }
  }

  public IArrowArray CurrentSegment => _chunkedArray.ArrowArray(_arrayIndex);

  public int SegmentLength => (_segmentEnd - _segmentBegin).ToIntExact();

  public int RelativeBegin => (_segmentBegin - _segmentOffset).ToIntExact();
}


public class ZamboniIterator<TSrc> where TSrc : struct, IEquatable<TSrc> {
  private readonly IReadOnlyList<PrimitiveArray<TSrc>> _arrays;
  private int _arrayIndex = -1;
  private Int64 _segmentOffset = 0;
  private Int64 _segmentBegin = 0;
  private Int64 _segmentEnd = 0;


  public ZamboniIterator(IReadOnlyList<PrimitiveArray<TSrc>> arrays) {
    _arrays = arrays;
  }

  public void Advance(Int64 start) {
    while (true) {
      if (start < _segmentBegin) {
        throw new Exception($"Programming error: Can't go backwards from {_segmentBegin} to {start}");
      }

      if (start < _segmentEnd) {
        // satisfiable with current segment
        _segmentBegin = start;
        return;
      }

      // Go to next array slice (or the first one, if this is the first call to Advance)
      ++_arrayIndex;
      if (_arrayIndex >= _arrays.Count) {
        throw new Exception($"Ran out of src data before processing all of RowSequence");
      }

      _segmentBegin = _segmentEnd;
      _segmentEnd = _segmentBegin + _arrays[_arrayIndex ].Length;
      _segmentOffset = _segmentBegin;
    }
  }

  public int CopyTo<TDest>(Span<TDest> dest, Span<bool> nulls, int requestedLength, Func<TSrc, TDest> converter) {
    var available = (_segmentEnd - _segmentBegin).ToIntExact();
    var amountToCopy = Math.Min(requestedLength, available);

    var array = _arrays[_arrayIndex];

    var segBegin = (_segmentBegin - _segmentOffset).ToIntExact();
    var arraySpan = array.Values.Slice(segBegin, amountToCopy);

    for (int i = 0; i != amountToCopy; ++i) {
      dest[i] = converter(arraySpan[i]);
    }

    if (nulls != null) {
      for (var i = 0; i != amountToCopy; ++i) {
        nulls[i] = array.IsNull(segBegin + i);
      }
    }

    return amountToCopy;
  }
}