//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

using System.Collections.Immutable;
using Apache.Arrow;
using Apache.Arrow.Types;

namespace Deephaven.Dh_NetClient;

public static class ArrowArrayConverter {
  public static Apache.Arrow.IArrowArray ColumnSourceToArray(IColumnSource columnSource, Int64 numRows) {
    var numRowsAsInt = numRows.ToIntExact();
    var rs = RowSequence.CreateSequential(Interval.OfStartAndSize(0, (UInt64)numRows));
    var chunk = ChunkMaker.CreateChunkFor(columnSource, numRowsAsInt);
    var nulls = BooleanChunk.Create(numRowsAsInt);
    columnSource.FillChunk(rs, chunk, nulls);
    var visitor = new ColumnSourceToArrowArrayVisitor(numRowsAsInt, chunk, nulls);
    columnSource.Accept(visitor);
    return visitor.Result!;
  }

  private class ColumnSourceToArrowArrayVisitor :
    IColumnSourceVisitor,
    IColumnSourceVisitor<ICharColumnSource>,
    IColumnSourceVisitor<IByteColumnSource>,
    IColumnSourceVisitor<IInt16ColumnSource>,
    IColumnSourceVisitor<IInt32ColumnSource>,
    IColumnSourceVisitor<IInt64ColumnSource>,
    IColumnSourceVisitor<IFloatColumnSource>,
    IColumnSourceVisitor<IDoubleColumnSource>,
    IColumnSourceVisitor<IStringColumnSource>,
    IColumnSourceVisitor<IBooleanColumnSource>,
    IColumnSourceVisitor<IDateTimeOffsetColumnSource>,
    IColumnSourceVisitor<IDateOnlyColumnSource>,
    IColumnSourceVisitor<ITimeOnlyColumnSource>,
    IColumnSourceVisitor<IListColumnSource> {

    private readonly int _numRows;
    private readonly Chunk _data;
    private readonly BooleanChunk _nulls;

    public Apache.Arrow.IArrowArray? Result = null;

    public ColumnSourceToArrowArrayVisitor(int numRows, Chunk data, BooleanChunk nulls) {
      _numRows = numRows;
      _data = data;
      _nulls = nulls;
    }

    public void Visit(IByteColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.Int8Array.Builder();
      CopyHelper<sbyte, Apache.Arrow.Int8Array, Apache.Arrow.Int8Array.Builder>(
        arrowBuilder);
    }

    public void Visit(IInt16ColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.Int16Array.Builder();
      CopyHelper<Int16, Apache.Arrow.Int16Array, Apache.Arrow.Int16Array.Builder>(
        arrowBuilder);
    }

    public void Visit(IInt32ColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.Int32Array.Builder();
      CopyHelper<Int32, Apache.Arrow.Int32Array, Apache.Arrow.Int32Array.Builder>(
        arrowBuilder);
    }

    public void Visit(IInt64ColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.Int64Array.Builder();
      CopyHelper<Int64, Apache.Arrow.Int64Array, Apache.Arrow.Int64Array.Builder>(
        arrowBuilder);
    }

    public void Visit(IFloatColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.FloatArray.Builder();
      CopyHelper<float, Apache.Arrow.FloatArray, Apache.Arrow.FloatArray.Builder>(
        arrowBuilder);
    }

    public void Visit(IDoubleColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.DoubleArray.Builder();
      CopyHelper<double, Apache.Arrow.DoubleArray, Apache.Arrow.DoubleArray.Builder>(
        arrowBuilder);
    }

    public void Visit(IBooleanColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.BooleanArray.Builder();
      CopyHelper<bool, Apache.Arrow.BooleanArray, Apache.Arrow.BooleanArray.Builder>(
        arrowBuilder);
    }

    public void Visit(IDateTimeOffsetColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.TimestampArray.Builder(TimeUnit.Nanosecond, "UTC");
      CopyHelper<DateTimeOffset, Apache.Arrow.TimestampArray, Apache.Arrow.TimestampArray.Builder>(arrowBuilder);
    }

    public void Visit(IDateOnlyColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.Date64Array.Builder();
      CopyHelper<DateOnly, Apache.Arrow.Date64Array, Apache.Arrow.Date64Array.Builder>(arrowBuilder);
    }

    public void Visit(ITimeOnlyColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.Date64Array.Builder();
      CopyHelper<DateOnly, Apache.Arrow.Date64Array, Apache.Arrow.Date64Array.Builder>(arrowBuilder);
    }

    public void Visit(ICharColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.UInt16Array.Builder();
      var typedData = ((CharChunk)_data).Data;
      for (var i = 0; i != _numRows; ++i) {
        if (!_nulls.Data[i]) {
          arrowBuilder.Append(typedData[i]);
        } else {
          arrowBuilder.AppendNull();
        }
      }

      Result = arrowBuilder.Build();
    }

    public void Visit(IStringColumnSource cs) {
      var arrowBuilder = new Apache.Arrow.StringArray.Builder();
      var typedData = ((StringChunk)_data).Data;
      for (var i = 0; i != _numRows; ++i) {
        if (!_nulls.Data[i]) {
          arrowBuilder.Append(typedData[i]);
        } else {
          arrowBuilder.AppendNull();
        }
      }

      Result = arrowBuilder.Build();
    }

    public void Visit(IListColumnSource cs) {
      var qqq = ZBlonga<int?>(cs);
      // var cb = TableMaker.ColumnBuilder.ForType<IList<int>>(null);
      // cb.Append([1, 2, 3]);
      // cb.Append([4, 5]);
      // cb.AppendNull();
      // Result = cb.Build();

      // var elementType = cs.ElementType;
      // var arrowElementType = ArrowTypeConverter.ToArrowType(elementType);
      // var arrowBuilder = new Apache.Arrow.ListArray.Builder(arrowElementType);
      // var underlyingBuilder = arrowBuilder.ValueBuilder;
      // var typedData = ((StringChunk)_data).Data;
      // for (var i = 0; i != _numRows; ++i) {
      //   if (!_nulls.Data[i]) {
      //     underlyingBuilder.Append(typedData[i]);
      //   } else {
      //
      //     arrowBuilder.AppendNull();
      //   }
      // }
      
      // Result = arrowBuilder.Build();
    }

    public IArrowArray ZBlonga<TElement>(IListColumnSource cs) {
      const int maxChunkSize = 512;

      var rowNum = 0;
      var remaining = _numRows;
      var dest = Chunk<System.Collections.IList>.Create(maxChunkSize);

      var nullFlags = BooleanChunk.Create(maxChunkSize);
      var cb = TableMaker.ColumnBuilder.ForType<IList<TElement>>(null);
      while (remaining != 0) {
        var nextChunkSize = Math.Min(remaining, maxChunkSize);
        var rows = RowSequence.CreateSequential(Interval.OfStartAndSize((UInt64)rowNum, (UInt64)nextChunkSize));
        cs.FillChunk(rows, dest, nullFlags);

        for (var i = 0; i != nextChunkSize; ++i) {
          if (!nullFlags.Data[i]) {
            cb.Append((IList<TElement>)dest.Data[i]);
          } else {
            cb.AppendNull();
          }
        }

        rowNum += nextChunkSize;
        remaining -= nextChunkSize;
      }

      return cb.Build();
    }

    private void CopyHelper<T, TArray, TBuilder>(TBuilder arrowBuilder)
      where TArray : Apache.Arrow.IArrowArray
      where TBuilder : Apache.Arrow.IArrowArrayBuilder<T, TArray, TBuilder> {
      var typedData = ((Chunk<T>)_data).Data;
      for (var i = 0; i != _numRows; ++i) {
        if (!_nulls.Data[i]) {
          arrowBuilder.Append(typedData[i]);
        } else {
          arrowBuilder.AppendNull();
        }
      }

      Result = arrowBuilder.Build(null);
    }

    public void Visit(IColumnSource cs) {
      throw new NotImplementedException($"No {nameof(ColumnSourceToArrowArrayVisitor)}.Visit for {Utility.FriendlyTypeName(cs.GetType())}");
    }

  }
}
