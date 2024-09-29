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

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, v => (char)v);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int16ArrowColumnSource : IInt16ColumnSource {
  public static Int16ArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<Int16Array>(chunkedArray);
    return new Int16ArrowColumnSource(arrays);
  }

  private readonly Int16Array[] _arrays;

  private Int16ArrowColumnSource(Int16Array[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, v => v);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int32ArrowColumnSource : IInt32ColumnSource {
  public static Int32ArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<Int32Array>(chunkedArray);
    return new Int32ArrowColumnSource(arrays);
  }

  private readonly Int32Array[] _arrays;

  private Int32ArrowColumnSource(Int32Array[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, v => v);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int64ArrowColumnSource : IInt64ColumnSource {
  public static Int64ArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = ZamboniHelpers.CastChunkedArray<Int64Array>(chunkedArray);
    return new Int64ArrowColumnSource(arrays);
  }

  private readonly Int64Array[] _arrays;

  private Int64ArrowColumnSource(Int64Array[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, v => v);
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
    var typedDest = (GenericChunk<TChunkElement>)destData;
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