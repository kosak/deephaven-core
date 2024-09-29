using Apache.Arrow;

namespace Deephaven.ManagedClient;

public class CharArrowColumnSource(ChunkedArray chunkedArray) : ICharColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, Chunk<bool>? nullFlags) {
    // var typedDest = (CharChunk)destData;
    // var pac = new TransformingArrayCopier<char>(typedDest, nullFlags);
    // Zamboni2Helpers.FillChunk(rows, chunkedArray, pac.DoCopy);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class ByteArrowColumnSource(ChunkedArray chunkedArray) : IByteColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, Chunk<bool>? nullFlags) {
    var typedDest = (ByteChunk)destData;
    var pac = new PrimitiveArrayCopier<sbyte>(typedDest, nullFlags);
    Zamboni2Helpers.FillChunk(rows, chunkedArray, pac.DoCopy);
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

public class FloatArrowColumnSource(ChunkedArray chunkedArray) : IFloatColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var typedDest = (FloatChunk)destData;
    var pac = new PrimitiveArrayCopier<float>(typedDest, nullFlags);
    Zamboni2Helpers.FillChunk(rows, chunkedArray, pac.DoCopy);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class DoubleArrowColumnSource(ChunkedArray chunkedArray) : IDoubleColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var hc = new ValueCopier<double>((DoubleChunk)destData, nullFlags);
    Zamboni2Helpers.FillChunk(rows, chunkedArray, hc.DoCopy);
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
    var hc = new ReferenceCopier<string>((StringChunk)destData, nullFlags);
    Zamboni2Helpers.FillChunk(rows, chunkedArray, hc.DoCopy);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}


public class TimestampArrowColumnSource(ChunkedArray chunkedArray) : ITimestampColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    // ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, DhDateTime.FromNanos);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class LocalDateArrowColumnSource(ChunkedArray chunkedArray) : ILocalDateColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    // ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, LocalDate.FromMillis);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class LocalTimeArrowColumnSource(ChunkedArray chunkedArray) : ILocalTimeColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    // ZamboniHelpers.FillChunk(rows, _arrays, destData, nullFlags, LocalTime.FromNanos);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

class ValueCopier<T>(Chunk<T> typedDest, BooleanChunk? nullFlags) where T : struct {
  public void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
    var typedSrc = (IReadOnlyList<T?>)src;
    for (var i = 0; i < count; ++i) {
      var value = typedSrc[srcOffset];
      typedDest.Data[destOffset] = value ?? default;
      if (nullFlags != null) {
        nullFlags.Data[destOffset] = !value.HasValue;
      }

      ++srcOffset;
      ++destOffset;
    }
  }
}

class ReferenceCopier<T>(Chunk<T> typedDest, BooleanChunk? nullFlags) {
  public void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
    var typedSrc = (IReadOnlyList<T>)src;
    for (var i = 0; i < count; ++i) {
      typedDest.Data[destOffset] = typedSrc[srcOffset];
      if (nullFlags != null) {
        nullFlags.Data[destOffset] = src.IsNull(srcOffset);
      }

      ++srcOffset;
      ++destOffset;
    }
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
