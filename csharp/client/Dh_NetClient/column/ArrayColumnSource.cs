//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
global using BooleanArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<bool>;
global using StringArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<string>;
global using CharArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<char>;
global using ByteArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<sbyte>;
global using Int16ArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<System.Int16>;
global using Int32ArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<System.Int32>;
global using Int64ArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<System.Int64>;
global using FloatArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<float>;
global using DoubleArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<double>;
global using DateTimeOffsetColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<System.DateTimeOffset>;
global using DateOnlyArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<System.DateOnly>;
global using TimeOnlyArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<System.TimeOnly>;
global using IListArrayColumnSource = Deephaven.Dh_NetClient.ArrayColumnSource<System.Collections.IList>;

using Apache.Arrow.Types;

namespace Deephaven.Dh_NetClient;

public abstract class ArrayColumnSource(int size) : IMutableColumnSource {
  public static ArrayColumnSource CreateFromArrowType(IArrowType type, int size) {
    var visitor = new ArrayColumnSourceMaker(size);
    type.Accept(visitor);
    return visitor.Result!;
  }

  protected bool[] Nulls = new bool[size];

  public abstract void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags);
  public abstract void FillFromChunk(RowSequence rows, Chunk src, BooleanChunk? nullFlags);

  public abstract void Accept(IColumnSourceVisitor visitor);

  public abstract ArrayColumnSource CreateOfSameType(int size);

  /// <summary>
  /// Ensures the source can hold at least 'capacity' elements, growing its backing storage if it cannot.
  ///
  /// Growth is geometric, which is what keeps a filling snapshot linear. A snapshot arrives as many
  /// successive batches, and the alternative - allocating an exactly-sized array per batch and copying
  /// the whole table into it - makes the total work quadratic in the number of rows: the Nth batch copies
  /// all N-1 batches that preceded it. Doubling instead amortises to O(1) copies per element.
  /// </summary>
  public abstract void EnsureCapacity(int capacity);

  /// <summary>
  /// Grows an array geometrically to hold at least 'capacity' elements, preserving 'used' leading elements.
  /// Returns the original array when it is already large enough.
  /// </summary>
  protected static T?[] Grown<T>(T?[] data, int capacity, int used) {
    if (capacity <= data.Length) {
      return data;
    }

    var newCapacity = Math.Max(data.Length == 0 ? 16 : data.Length * 2, capacity);
    var grown = new T?[newCapacity];
    Array.Copy(data, grown, used);
    return grown;
  }

  private class ArrayColumnSourceMaker(int size) :
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
      Result = new DateTimeOffsetColumnSource(size);
    }

    public void Visit(Date64Type type) {
      Result = new DateOnlyArrayColumnSource(size);
    }

    public void Visit(Time64Type type) {
      Result = new TimeOnlyArrayColumnSource(size);
    }

    public void Visit(ListType type) {
      Result = new ListArrayColumnSource(type.ValueDataType, size);
    }

    public void Visit(IArrowType type) {
      throw new Exception($"ArrayColumnSource: type {type.Name} is not supported");
    }
  }
}

public sealed class ArrayColumnSource<T>(int size) : ArrayColumnSource(size), IMutableColumnSource<T> {
  private T?[] _data = new T?[size];

  /// <summary>
  /// How much of _data is live. Backing storage is grown geometrically so it usually runs past this point;
  /// only the leading _used elements have to be preserved when it grows again.
  /// </summary>
  private int _used = size;

  public override void EnsureCapacity(int capacity) {
    _data = Grown(_data, capacity, _used);
    Nulls = Grown(Nulls, capacity, _used)!;
    if (capacity > _used) {
      _used = capacity;
    }
  }

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

public sealed class ListArrayColumnSource : ArrayColumnSource, IMutableColumnSource<System.Collections.IList>, IHasElementType {
  private readonly System.Collections.IList?[] _data;
  public Type ElementType { get; }

  public ListArrayColumnSource(IArrowType valueDataType, int size) : this(GetElementType(valueDataType), size) {}

  private ListArrayColumnSource(Type elementType, int size) : base(size) {
    _data = new System.Collections.IList?[size];
    ElementType = elementType;
  }

  public override void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags) {
    var typedChunk = (ListChunk)dest;
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
    var typedChunk = (ListChunk)src;
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
    return new ListArrayColumnSource(ElementType, size);
  }

  private static Type GetElementType(IArrowType valueDataType) {
    var visitor = new ElementTypeVisitor();
    valueDataType.Accept(visitor);
    return visitor.Result ?? throw new Exception($"type {valueDataType.Name} is not supported");
  }
}
