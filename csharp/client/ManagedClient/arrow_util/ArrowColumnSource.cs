using Apache.Arrow;

namespace Deephaven.ManagedClient;

public class CharArrowColumnSource(ChunkedArray chunkedArray) : ICharColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, Chunk<bool>? nullFlags) {
    var tc = new TransformingCopier<UInt16, char>((CharChunk)destData, nullFlags, v => (char)v);
    tc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class ByteArrowColumnSource(ChunkedArray chunkedArray) : IByteColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, Chunk<bool>? nullFlags) {
    var vc = new ValueCopier<sbyte>((ByteChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int16ArrowColumnSource(ChunkedArray chunkedArray) : IInt16ColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var vc = new ValueCopier<Int16>((Int16Chunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int32ArrowColumnSource(ChunkedArray chunkedArray) : IInt32ColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var vc = new ValueCopier<Int32>((Int32Chunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class Int64ArrowColumnSource(ChunkedArray chunkedArray) : IInt64ColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var vc = new ValueCopier<Int64>((Int64Chunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class FloatArrowColumnSource(ChunkedArray chunkedArray) : IFloatColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var vc = new ValueCopier<float>((FloatChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class DoubleArrowColumnSource(ChunkedArray chunkedArray) : IDoubleColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var vc = new ValueCopier<double>((DoubleChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class BooleanArrowColumnSource(ChunkedArray chunkedArray) : IBooleanColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var vc = new ValueCopier<bool>((BooleanChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class StringArrowColumnSource(ChunkedArray chunkedArray) : IStringColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var rc = new ReferenceCopier<string>((StringChunk)destData, nullFlags);
    rc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}


public class TimestampArrowColumnSource(ChunkedArray chunkedArray) : ITimestampColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var tc = new TransformingCopier<Int64, DhDateTime>((DhDateTimeChunk)destData, nullFlags, DhDateTime.FromNanos);
    tc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class LocalDateArrowColumnSource(ChunkedArray chunkedArray) : ILocalDateColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var tc = new TransformingCopier<Int64, LocalDate>((LocalDateChunk)destData, nullFlags, LocalDate.FromMillis);
    tc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class LocalTimeArrowColumnSource(ChunkedArray chunkedArray) : ILocalTimeColumnSource {
  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    var tc = new TransformingCopier<Int64, LocalTime>((LocalTimeChunk)destData, nullFlags, LocalTime.FromNanos);
    tc.FillChunk(rows, chunkedArray);
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

abstract class FillChunkHelper {
  public void FillChunk(RowSequence rows, ChunkedArray srcArray) {
    if (rows.Empty) {
      return;
    }

    var srcIterator = new ChunkedArrayIterator(srcArray);
    var destIndex = 0;

    foreach (var (reqBeginConst, reqEnd) in rows.Intervals) {
      var reqBegin = reqBeginConst;
      while (true) {
        var reqLength = (reqEnd - reqBegin).ToIntExact();
        if (reqLength == 0) {
          return;
        }

        srcIterator.Advance(checked((Int64)reqBegin));
        var amountToCopy = Math.Min(reqLength, srcIterator.SegmentLength);
        DoCopy(srcIterator.CurrentSegment, srcIterator.RelativeBegin, destIndex, amountToCopy);

        reqBegin += (UInt64)amountToCopy;
        destIndex += amountToCopy;
      }
    }
  }

  protected abstract void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count);
}

sealed class ValueCopier<T>(Chunk<T> typedDest, BooleanChunk? nullFlags) : FillChunkHelper where T : struct {
  protected override void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
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

sealed class TransformingCopier<TSrc, TDest>(Chunk<TDest> typedDest, BooleanChunk? nullFlags, Func<TSrc, TDest> transformer)
  : FillChunkHelper where TSrc : struct where TDest : struct {
  protected override void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
    var typedSrc = (IReadOnlyList<TSrc?>)src;
    for (var i = 0; i < count; ++i) {
      var value = typedSrc[srcOffset];
      typedDest.Data[destOffset] = value.HasValue ? transformer(value.Value) : default;
      if (nullFlags != null) {
        nullFlags.Data[destOffset] = !value.HasValue;
      }

      ++srcOffset;
      ++destOffset;
    }
  }
}

sealed class ReferenceCopier<T>(Chunk<T> typedDest, BooleanChunk? nullFlags) : FillChunkHelper {
  protected override void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
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

public class ChunkedArrayIterator(ChunkedArray chunkedArray) {
  private int _arrayIndex = -1;
  private Int64 _segmentOffset = 0;
  private Int64 _segmentBegin = 0;
  private Int64 _segmentEnd = 0;

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
      if (_arrayIndex >= chunkedArray.ArrayCount) {
        throw new Exception($"Ran out of src data before processing all of RowSequence");
      }

      _segmentBegin = _segmentEnd;
      _segmentEnd = _segmentBegin + chunkedArray.ArrowArray(_arrayIndex).Length;
      _segmentOffset = _segmentBegin;
    }
  }

  public IArrowArray CurrentSegment => chunkedArray.ArrowArray(_arrayIndex);

  public int SegmentLength => (_segmentEnd - _segmentBegin).ToIntExact();

  public int RelativeBegin => (_segmentBegin - _segmentOffset).ToIntExact();
}
