//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//

global using BooleanArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<bool>;
global using StringArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<string>;
global using CharArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<char>;
global using ByteArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<sbyte>;
global using Int16ArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<System.Int16>;
global using Int32ArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<System.Int32>;
global using Int64ArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<System.Int64>;
global using FloatArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<float>;
global using DoubleArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<double>;
global using DateTimeOffsetArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<System.DateTimeOffset>;
global using LocalDateArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<System.DateOnly>;
global using LocalTimeArrowColumnSource = Deephaven.Dh_NetClient.ArrowColumnSource<System.TimeOnly>;
using System.Collections;
using Apache.Arrow;
using Apache.Arrow.Types;
using Array = System.Array;

namespace Deephaven.Dh_NetClient;

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
    IColumnSourceVisitor<IDateTimeOffsetColumnSource>,
    IColumnSourceVisitor<IDateOnlyColumnSource>,
    IColumnSourceVisitor<ITimeOnlyColumnSource> {
  public void Visit(ICharColumnSource src) {
    var tc = new TransformingCopier<UInt16, char>((CharChunk)destData, nullFlags,
      (UInt16)DeephavenConstants.NullChar, DeephavenConstants.NullChar, v => (char)v);
    tc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IByteColumnSource _) {
    var vc = new ValueCopier<sbyte>((ByteChunk)destData, nullFlags,
      DeephavenConstants.NullByte);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IInt16ColumnSource _) {
    var vc = new ValueCopier<Int16>((Int16Chunk)destData, nullFlags,
      DeephavenConstants.NullShort);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IInt32ColumnSource _) {
    var vc = new ValueCopier<Int32>((Int32Chunk)destData, nullFlags,
      DeephavenConstants.NullInt);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IInt64ColumnSource _) {
    var vc = new ValueCopier<Int64>((Int64Chunk)destData, nullFlags,
      DeephavenConstants.NullLong);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IFloatColumnSource _) {
    var vc = new ValueCopier<float>((FloatChunk)destData, nullFlags,
      DeephavenConstants.NullFloat);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IDoubleColumnSource _) {
    var vc = new ValueCopier<double>((DoubleChunk)destData, nullFlags,
      DeephavenConstants.NullDouble);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IBooleanColumnSource _) {
    var vc = new ValueCopier<bool>((BooleanChunk)destData, nullFlags, null);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IStringColumnSource _) {
    var vc = new ReferenceCopier<string>((StringChunk)destData, nullFlags);
    vc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IDateTimeOffsetColumnSource _) {
    var tc = new TransformingCopier<Int64, DateTimeOffset>((DateTimeOffsetChunk)destData,
      nullFlags, DeephavenConstants.NullLong, new DateTimeOffset(),
      nanos => DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(nanos / TimeSpan.NanosecondsPerTick));
    tc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IDateOnlyColumnSource _) {
    var tc = new TransformingCopier<Int64, DateOnly>((DateOnlyChunk)destData,
      nullFlags, DeephavenConstants.NullLong, new DateOnly(),
      millis => {
        var dto = DateTimeOffset.UnixEpoch + TimeSpan.FromMilliseconds(millis);
        return DateOnly.FromDateTime(dto.DateTime);
      });
    tc.FillChunk(rows, chunkedArray);
  }

  public void Visit(ITimeOnlyColumnSource _) {
    var tc = new TransformingCopier<Int64, TimeOnly>((TimeOnlyChunk)destData,
      nullFlags, DeephavenConstants.NullLong, new TimeOnly(),
      nanos => TimeOnly.FromTimeSpan(TimeSpan.FromTicks(nanos / TimeSpan.NanosecondsPerTick)));
    tc.FillChunk(rows, chunkedArray);
  }

  public void Visit(IColumnSource cs) {
    throw new Exception($"IColumnSource type {cs.GetType().Name} not implemented");
  }
}

abstract class FillChunkHelper {
  public void FillChunk(RowSequence rows, ChunkedArray srcArray) {
    if (rows.IsEmpty) {
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

sealed class ValueCopier<T>(Chunk<T> typedDest, BooleanChunk? nullFlags, T? deephavenNullValue)
  : FillChunkHelper where T : struct {
  protected override void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
    var typedSrc = (IReadOnlyList<T?>)src;
    for (var i = 0; i < count; ++i) {
      var value = typedSrc[srcOffset];
      var isNull = !value.HasValue || value.Value.Equals(deephavenNullValue);
      T destToUse;
      if (value.HasValue) {
        destToUse = value.Value;
      } else if (deephavenNullValue.HasValue) {
        destToUse = deephavenNullValue.Value;
      } else {
        destToUse = default;
      }
      typedDest.Data[destOffset] = destToUse;

      if (nullFlags != null) {
        // It looks like even though Deephaven is correctly setting the null bitmap when
        // it comes through DoGet, we're not getting null values when it comes through Barrage.
        nullFlags.Data[destOffset] = isNull;
      }

      ++srcOffset;
      ++destOffset;
    }
  }
}

sealed class TransformingCopier<TSrc, TDest>(
  Chunk<TDest> typedDest,
  BooleanChunk? nullFlags,
  TSrc deephavenNullValue,
  TDest transformedNullValue,
  Func<TSrc, TDest> transformer)
  : FillChunkHelper where TSrc : struct where TDest : struct {
  protected override void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
    var typedSrc = (IReadOnlyList<TSrc?>)src;
    for (var i = 0; i < count; ++i) {
      var value = typedSrc[srcOffset];
      bool isNull;
      if (!value.HasValue || value.Value.Equals(deephavenNullValue)) {
        isNull = true;
        typedDest.Data[destOffset] = transformedNullValue;
      } else {
        isNull = false;
        typedDest.Data[destOffset] = transformer(value.Value);
      }

      if (nullFlags != null) {
        nullFlags.Data[destOffset] = isNull;
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

sealed class ListCopier(ListChunk typedDest, BooleanChunk? nullFlags) : FillChunkHelper {
  protected override void DoCopy(IArrowArray src, int srcOffset, int destOffset, int count) {
    var typedSrc = (ListArray)src;
    // var srcValues = (Apache.Arrow.Array)typedSrc.Values;
    for (var i = 0; i < count; ++i, ++srcOffset, ++destOffset) {
      if (src.IsNull(i)) {
        if (nullFlags != null) {
          typedDest.Data[destOffset] = null;
          nullFlags.Data[destOffset] = true;
        }
        continue;
      }

      var slicedData = typedSrc.GetSlicedValues(srcOffset);

      // var start = typedSrc.ValueOffsets[srcOffset];
      // var end = typedSrc.ValueOffsets[srcOffset + 1];
      // var slicedData = srcValues.Slice(start, end - start);
      var sn = new SuperNubbin();
      slicedData.Accept(sn);
      typedDest.Data[destOffset] = sn.Result;
    }
  }
}

public class SuperNubbin : IArrowArrayVisitor,
  IArrowArrayVisitor<UInt16Array>,
  IArrowArrayVisitor<Int8Array>,
  IArrowArrayVisitor<Int16Array>,
  IArrowArrayVisitor<Int32Array>,
  IArrowArrayVisitor<Int64Array>,
  IArrowArrayVisitor<FloatArray>,
  IArrowArrayVisitor<DoubleArray>,
  IArrowArrayVisitor<StringArray>,
  IArrowArrayVisitor<BooleanArray>,
  IArrowArrayVisitor<TimestampArray>,
  IArrowArrayVisitor<Date64Array>,
  IArrowArrayVisitor<Time64Array> {

  public IList Result { get; private set; } = new List<int>();

  public void Visit(UInt16Array array) {
    Result = new KosakArray<UInt16>(array, DeephavenConstants.NullChar);
  }

  public void Visit(Int8Array array) {
    Result = new KosakArray<SByte>(array, DeephavenConstants.NullByte);
  }

  public void Visit(Int16Array array) {
    Result = new KosakArray<Int16>(array, DeephavenConstants.NullShort);
  }

  public void Visit(Int32Array array) {
    Result = new KosakArray<Int32>(array, DeephavenConstants.NullInt);
  }

  public void Visit(Int64Array array) {
    Result = new KosakArray<Int64>(array, DeephavenConstants.NullLong);
  }

  public void Visit(FloatArray array) {
    Result = new KosakArray<float>(array, DeephavenConstants.NullFloat);
  }

  public void Visit(DoubleArray array) {
    Result = new KosakArray<double>(array, DeephavenConstants.NullDouble);
  }

  public void Visit(StringArray array) {
    throw new NotImplementedException("TODO");
  }

  public void Visit(BooleanArray array) {
    Result = new KosakArray<bool>(array, null);
  }

  public void Visit(TimestampArray array) {
    Result = new KosakArray<DateTimeOffset>(array, new DateTimeOffset());
  }

  public void Visit(Date64Array array) {
    Result = new KosakArray<DateOnly>(array, new DateOnly());
  }

  public void Visit(Time64Array array) {
    Result = new KosakArray<TimeOnly>(array, new TimeOnly());
  }

  public void Visit(IArrowArray array) {
    throw new NotImplementedException("Client does not support multiple levels of array nesting");
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
        throw new Exception($"Assertion failed: Can't go backwards from {_segmentBegin} to {start}");
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


public class KosakArray<T> : IList, IList<T>, IList<T?> where T : struct, IEquatable<T> {
  private readonly IReadOnlyList<T?> _data;
  private readonly T _deephavenNullValue;

  public KosakArray(IReadOnlyList<T?> data, T deephavenNullValue) {
    _data = data;
    _deephavenNullValue = deephavenNullValue;
  }


  int IList.Add(object? item) => NotImplementedForReadOnlyList<int>();
  void ICollection<T>.Add(T item) => NotImplementedForReadOnlyList<bool>();
  void ICollection<T?>.Add(T? item) => NotImplementedForReadOnlyList<bool>();

  public void Clear() => NotImplementedForReadOnlyList<int>();

  bool IList.Contains(object? value) => ((IList)this).IndexOf(value) >= 0;
  bool ICollection<T>.Contains(T item) => ((IList<T>)this).IndexOf(item) >= 0;
  bool ICollection<T?>.Contains(T? item) => ((IList<T?>)this).IndexOf(item) >= 0;

  int IList.IndexOf(object? value) {
    if (value == null) {
      return ((IList<T?>)this).IndexOf(null);
    }
    if (value is T value1) {
      return ((IList<T?>)this).IndexOf(value1);
    }
    return -1;
  }

  int IList<T>.IndexOf(T value) {
    var valueToCheck = value.Equals(_deephavenNullValue) ? (T?)null : value;
    return ((IList<T?>)this).IndexOf(valueToCheck);
  }

  int IList<T?>.IndexOf(T? value) {
    for (var i = 0; i < _data.Count; ++i) {
      if (Nullable.Equals(_data[i], value)) {
        return i;
      }
    }
    return -1;
  }

  void IList.Insert(int index, object? value) => NotImplementedForReadOnlyList<bool>();
  void IList<T>.Insert(int index, T item) => NotImplementedForReadOnlyList<bool>();
  void IList<T?>.Insert(int index, T? item) => NotImplementedForReadOnlyList<bool>();

  void IList.Remove(object? value) => NotImplementedForReadOnlyList<bool>();
  bool ICollection<T>.Remove(T item) => NotImplementedForReadOnlyList<bool>();
  bool ICollection<T?>.Remove(T? item) => NotImplementedForReadOnlyList<bool>();

  public void RemoveAt(int index) => NotImplementedForReadOnlyList<bool>();

  bool IList.IsFixedSize => true;
  public bool IsReadOnly => true;

  void ICollection.CopyTo(Array array, int index) {
    throw new NotImplementedException();
  }
  void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
    throw new NotImplementedException();
  }
  void ICollection<T?>.CopyTo(T?[] array, int arrayIndex) {
    throw new NotImplementedException();
  }

  public int Count => _data.Count;

  public bool IsSynchronized => false;
  public object SyncRoot => this;

  object? IList.this[int index] {
    get {
      var value = _data[index];
      return value ?? _deephavenNullValue;
    }
    set => _ = NotImplementedForReadOnlyList<bool>();
  }

  T IList<T>.this[int index] {
    get {
      var value = _data[index];
      return value ?? _deephavenNullValue;
    }
    set => _ = NotImplementedForReadOnlyList<bool>();
  }

  T? IList<T?>.this[int index] {
    get => _data[index];
    set => _ = NotImplementedForReadOnlyList<bool>();
  }

  IEnumerator IEnumerable.GetEnumerator() {
    return _data.Select(item => item ?? _deephavenNullValue).GetEnumerator();
  }

  IEnumerator<T> IEnumerable<T>.GetEnumerator() {
    return _data.Select(item => item ?? _deephavenNullValue).GetEnumerator();
  }

  IEnumerator<T?> IEnumerable<T?>.GetEnumerator() {
    return _data.GetEnumerator();
  }

  private U NotImplementedForReadOnlyList<U>() {
    throw new NotImplementedException("This method is not implemented because the data structure is readonly");
  }
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
  IArrowTypeVisitor<Time64Type>,
  IArrowTypeVisitor<ListType> {
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
    Result = new DateTimeOffsetArrowColumnSource(chunkedArray);
  }

  public void Visit(Date64Type type) {
    Result = new LocalDateArrowColumnSource(chunkedArray);
  }

  public void Visit(Time64Type type) {
    Result = new LocalTimeArrowColumnSource(chunkedArray);
  }

  public void Visit(ListType type) {
    var visitor = new ElementTypeVisitor();
    type.ValueDataType.Accept(visitor);
    var elementType = visitor.Result;
    Result = new ListArrowColumnSource(chunkedArray, elementType);
  }

  public void Visit(IArrowType type) {
    throw new Exception($"Arrow type {type.Name} is not supported");
  }
}

public class ElementTypeVisitor :
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

  public Type Result { get; private set; }

  public void Visit(UInt16Type type) {
    Result = typeof(char);
  }
  public void Visit(Int8Type type) {
    Result = typeof(SByte);
  }
  public void Visit(Int16Type type) {
    Result = typeof(Int16);
  }
  public void Visit(Int32Type type) {
    Result = typeof(Int32);
  }
  public void Visit(Int64Type type) {
    Result = typeof(Int64);
  }
  public void Visit(FloatType type) {
    Result = typeof(float);
  }
  public void Visit(DoubleType type) {
    Result = typeof(double);
  }
  public void Visit(BooleanType type) {
    Result = typeof(bool);
  }
  public void Visit(StringType type) {
    Result = typeof(string);
  }
  public void Visit(TimestampType type) {
    Result = typeof(DateTimeOffset);
  }
  public void Visit(Date64Type type) {
    Result = typeof(DateOnly);
  }
  public void Visit(Time64Type type) {
    Result = typeof(TimeOnly);
  }

  public void Visit(IArrowType type) {
    throw new Exception($"Arrow type {type.Name} is not supported");
  }
}

public class ListArrowColumnSource(ChunkedArray chunkedArray, Type elementType) : ArrowColumnSource, IListColumnSource, IHasElementType {
  public Type ElementType => elementType;

  public override void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags) {
    var typedDest = (ListChunk)dest;
    var lc = new ListCopier(typedDest, nullFlags);
    lc.FillChunk(rows, chunkedArray);
  }

  public override void Accept(IColumnSourceVisitor visitor) {
    IColumnSource.Accept(this, visitor);
  }

}