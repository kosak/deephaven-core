﻿global using BooleanArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<bool>;
global using StringArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<string>;
global using CharArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<char>;
global using ByteArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<sbyte>;
global using Int16ArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<System.Int16>;
global using Int32ArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<System.Int32>;
global using Int64ArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<System.Int64>;
global using FloatArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<float>;
global using DoubleArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<double>;
global using TimestampArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<Deephaven.ManagedClient.DhDateTime>;
global using LocalDateArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<Deephaven.ManagedClient.LocalDate>;
global using LocalTimeArrayColumnSource = Deephaven.ManagedClient.ArrayColumnSource<Deephaven.ManagedClient.LocalTime>;

using Apache.Arrow.Types;

namespace Deephaven.ManagedClient;

public abstract class ArrayColumnSource(int size) : IMutableColumnSource {
  public static ArrayColumnSource CreateFromArrowType(IArrowType type, int size) {
    var visitor = new ArrayColumnSourceMaker(size);
    type.Accept(visitor);
    return visitor.Result!;
  }

  protected readonly bool[] Nulls = new bool[size];

  public abstract void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags);
  public abstract void FillFromChunk(RowSequence rows, Chunk src, BooleanChunk? nullFlags);

  public abstract void Accept(IColumnSourceVisitor visitor);

  public abstract ArrayColumnSource CreateOfSameType(int size);
}

public sealed class ArrayColumnSource<T>(int size) : ArrayColumnSource(size), IMutableColumnSource<T> {
  private readonly T[] _data = new T[size];

  public override void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags) {
    var typedChunk = (Chunk<T>)dest;
    var nextIndex = 0;
    foreach (var (begin, end) in rows.Intervals) {
      for (var i = begin; i < end; ++i) {
        typedChunk.Data[nextIndex] = _data[i];
        if (nullFlags != null) {
          nullFlags.Data[nextIndex] = Nulls[i];
        }
        ++nextIndex;
      }
    }
  }

  public override void FillFromChunk(RowSequence rows, Chunk src, BooleanChunk? nullFlags) {
    var typedChunk = (Chunk<T>)src;
    var nextIndex = 0;
    foreach (var (begin, end) in rows.Intervals) {
      for (var i = begin; i < end; ++i) {
        _data[i] = typedChunk.Data[nextIndex];
        if (nullFlags != null) {
          Nulls[i] = nullFlags.Data[nextIndex];
        }
        ++nextIndex;
      }
    }
  }

  public override void Accept(IColumnSourceVisitor visitor) {
    IColumnSource.Accept(this, visitor);
  }

  public override ArrayColumnSource CreateOfSameType(int size) {
    return new ArrayColumnSource<T>(size);
  }
}

class ArrayColumnSourceMaker(int size) :
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
  public ArrayColumnSource? Result { get; private set; }

  public void Visit(UInt16Type type) {
    Result = new CharArrayColumnSource(size);
  }

  public void Visit(Int8Type type) {
    Result = new ByteArrayColumnSource(size);
  }

  public void Visit(Int16Type type) {
    Result = new Int16ArrayColumnSource(size);
  }

  public void Visit(Int32Type type) {
    Result = new Int32ArrayColumnSource(size);
  }

  public void Visit(Int64Type type) {
    Result = new Int64ArrayColumnSource(size);
  }

  public void Visit(FloatType type) {
    Result = new FloatArrayColumnSource(size);
  }

  public void Visit(DoubleType type) {
    Result = new DoubleArrayColumnSource(size);
  }

  public void Visit(BooleanType type) {
    Result = new BooleanArrayColumnSource(size);
  }

  public void Visit(StringType type) {
    Result = new StringArrayColumnSource(size);
  }

  public void Visit(TimestampType type) {
    Result = new TimestampArrayColumnSource(size);
  }

  public void Visit(Date64Type type) {
    Result = new LocalDateArrayColumnSource(size);
  }

  public void Visit(Time64Type type) {
    Result = new LocalTimeArrayColumnSource(size);
  }

  public void Visit(IArrowType type) {
    throw new Exception($"type {type.Name} is not supported");
  }
}