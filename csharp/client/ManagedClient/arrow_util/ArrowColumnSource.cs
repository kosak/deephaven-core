﻿global using BooleanArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<bool>;
global using StringArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<string>;
global using CharArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<char>;
global using ByteArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<sbyte>;
global using Int16ArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<System.Int16>;
global using Int32ArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<System.Int32>;
global using Int64ArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<System.Int64>;
global using FloatArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<float>;
global using DoubleArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<double>;
global using TimestampArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<Deephaven.ManagedClient.DhDateTime>;
global using LocalDateArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<Deephaven.ManagedClient.LocalDate>;
global using LocalTimeArrowColumnSource = Deephaven.ManagedClient.ArrowColumnSource<Deephaven.ManagedClient.LocalTime>;

using Apache.Arrow;
using Apache.Arrow.Types;

namespace Deephaven.ManagedClient;

public abstract class ArrowColumnSource : IColumnSource {
  public static ArrowColumnSource CreateFromColumn(Column column) {
    var visitor = new ArrowColumnSourceMaker(column.Data);
    column.Type.Accept(visitor);
    if (visitor.Result == null) {
      throw new Exception($"No result set for {column.Data.DataType}");
    }
    return visitor.Result;
  }

  public static (ArrowColumnSource, int) CreateFromListArray(ListArray la) {
    if (la.Length != 1) {
      throw new Exception($"Expected ListArray of length 1, got {la.Length}");
    }
    var array = la.GetSlicedValues(0);
    var chunkedArray = new ChunkedArray(new[] { array });

    var visitor = new ArrowColumnSourceMaker(chunkedArray);
    array.Data.DataType.Accept(visitor);
    return (visitor.Result!, array.Length);
  }

  public abstract void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags);
  public abstract void Accept(IColumnSourceVisitor visitor);
}

public sealed class ArrowColumnSource<T>(ChunkedArray chunkedArray) : ArrowColumnSource, IColumnSource<T> {
  public override void FillChunk(RowSequence rows, Chunk destData, Chunk<bool>? nullFlags) {
    var visitor = new FillChunkVisitor(chunkedArray, rows, destData, nullFlags);
    Accept(visitor);
  }

  public override void Accept(IColumnSourceVisitor visitor) {
    IColumnSource.Accept(this, visitor);
  }
}

class FillChunkVisitor(ChunkedArray chunkedArray, RowSequence rows, Chunk destData, Chunk<bool>? nullFlags)
  : IColumnSourceVisitor<ICharColumnSource>,
    IColumnSourceVisitor<IByteColumnSource>,
    IColumnSourceVisitor<IInt16ColumnSource>,
    IColumnSourceVisitor<IInt32ColumnSource>,
    IColumnSourceVisitor<IInt64ColumnSource>,
    IColumnSourceVisitor<IFloatColumnSource>,
    IColumnSourceVisitor<IDoubleColumnSource>,
    IColumnSourceVisitor<IBooleanColumnSource>,
    IColumnSourceVisitor<IStringColumnSource>,
    IColumnSourceVisitor<ITimestampColumnSource>,
    IColumnSourceVisitor<ILocalDateColumnSource>,
    IColumnSourceVisitor<ILocalTimeColumnSource> {
  public void Visit(ICharColumnSource src) {
    var tc = new TransformingCopier<UInt16, char>((CharChunk)destData, nullFlags, v => (char)v);
    tc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IByteColumnSource src) {
    var vc = new ValueCopier<sbyte>((ByteChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IInt16ColumnSource src) {
    var vc = new ValueCopier<Int16>((Int16Chunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IInt32ColumnSource src) {
    var vc = new ValueCopier<Int32>((Int32Chunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IInt64ColumnSource src) {
    var vc = new ValueCopier<Int64>((Int64Chunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IFloatColumnSource src) {
    var vc = new ValueCopier<float>((FloatChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IDoubleColumnSource src) {
    var vc = new ValueCopier<double>((DoubleChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IBooleanColumnSource src) {
    var vc = new ValueCopier<bool>((BooleanChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IStringColumnSource src) {
    var vc = new ReferenceCopier<string>((StringChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(ITimestampColumnSource src) {
    var tc = new TransformingCopier<Int64, DhDateTime>((DhDateTimeChunk)destData, nullFlags, DhDateTime.FromNanos);
    tc.FillChunk(rows, chunkedArray);
  }

  public void Visit(ILocalDateColumnSource src) {
    var tc = new TransformingCopier<Int64, LocalDate>((LocalDateChunk)destData, nullFlags, LocalDate.FromMillis);
    tc.FillChunk(rows, chunkedArray);
  }

  public void Visit(ILocalTimeColumnSource src) {
    var tc = new TransformingCopier<Int64, LocalTime>((LocalTimeChunk)destData, nullFlags, LocalTime.FromNanos);
    tc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IColumnSource cs) {
    throw new Exception($"IColumnSource type {cs.GetType().Name} not implemented");
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

class ArrowColumnSourceMaker(ChunkedArray chunkedArray) :
  IArrowTypeVisitor<UInt16Type>,
  IArrowTypeVisitor<Int8Type>,
  IArrowTypeVisitor<Int16Type>,
  IArrowTypeVisitor<Int32Type>,
  IArrowTypeVisitor<Int64Type>,
  IArrowTypeVisitor<FloatType>,
  IArrowTypeVisitor<DoubleType>,
  IArrowTypeVisitor<BooleanType>,
  IArrowTypeVisitor<StringType>,
  IArrowTypeVisitor<TimestampType>,
  IArrowTypeVisitor<Date64Type>,
  IArrowTypeVisitor<Time64Type> {
  public ArrowColumnSource? Result { get; private set; }

  public void Visit(UInt16Type type) {
    Result = new CharArrowColumnSource(chunkedArray);
  }

  public void Visit(Int8Type type) {
    Result = new ByteArrowColumnSource(chunkedArray);
  }

  public void Visit(Int16Type type) {
    Result = new Int16ArrowColumnSource(chunkedArray);
  }

  public void Visit(Int32Type type) {
    Result = new Int32ArrowColumnSource(chunkedArray);
  }

  public void Visit(Int64Type type) {
    Result = new Int64ArrowColumnSource(chunkedArray);
  }

  public void Visit(FloatType type) {
    Result = new FloatArrowColumnSource(chunkedArray);
  }

  public void Visit(DoubleType type) {
    Result = new DoubleArrowColumnSource(chunkedArray);
  }

  public void Visit(BooleanType type) {
    Result = new BooleanArrowColumnSource(chunkedArray);
  }

  public void Visit(StringType type) {
    Result = new StringArrowColumnSource(chunkedArray);
  }

  public void Visit(TimestampType type) {
    Result = new TimestampArrowColumnSource(chunkedArray);
  }

  public void Visit(Date64Type type) {
    Result = new LocalDateArrowColumnSource(chunkedArray);
  }

  public void Visit(Time64Type type) {
    Result = new LocalTimeArrowColumnSource(chunkedArray);
  }

  public void Visit(IArrowType type) {
    throw new Exception($"type {type.Name} is not supported");
  }
}