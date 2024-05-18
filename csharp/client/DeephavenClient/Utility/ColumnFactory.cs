using Deephaven.DeephavenClient.Interop;
using System;
using System.Reflection;

namespace Deephaven.DeephavenClient.Utility;

internal abstract class ColumnFactory<TTableType> {
  public abstract (Array, bool[]) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
    Int64 numRows);

  public abstract Array GetNullableColumn(NativePtr<TTableType> table, Int32 columnIndex,
    Int64 numRows);

  public delegate void NativeImpl<in T>(NativePtr<TTableType> table, Int32 columnIndex,
    T[] data, sbyte[]? nullFlags, Int64 numRows, out ErrorStatus status);

  public sealed class ForString : ColumnFactory<TTableType> {
    private readonly NativeImpl<string> _nativeImpl;

    public ForString(NativeImpl<string> nativeImpl) => _nativeImpl = nativeImpl;

    public override (Array, bool[]) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      var data = new string[numRows];
      var nullsAsSbytes = new sbyte[numRows];
      _nativeImpl(table, columnIndex, data, nullsAsSbytes, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        nulls[i] = nullsAsSbytes[i] != 0;
      }

      return (data, nulls);
    }

    public override Array GetNullableColumn(NativePtr<TTableType> table, int columnIndex, long numRows) {
      // string is a reference type so there's no such thing as a Nullable<string>.
      // For the case of string, the return value is the same as GetColumn().
      return GetColumn(table, columnIndex, numRows).Item1;
    }
  }

  public abstract class ColumnFactoryForValueTypes<T> : ColumnFactory<TTableType> where T : struct {
    public sealed override (Array, bool[]) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      return GetColumnInternal(table, columnIndex, numRows);
    }

    public sealed override Array GetNullableColumn(NativePtr<TTableType> table, int columnIndex, long numRows) {
      var (data, nulls) = GetColumnInternal(table, columnIndex, numRows);
      var result = new T?[numRows];
      for (var i = 0; i != numRows; ++i) {
        if (!nulls[i]) {
          result[i] = data[i];
        }
      }

      return result;
    }

    protected abstract (T[], bool[]) GetColumnInternal(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows);
  }

  public sealed class ForBool : ColumnFactoryForValueTypes<bool> {
    private readonly NativeImpl<sbyte> _nativeImpl;

    public ForBool(NativeImpl<sbyte> nativeImpl) => _nativeImpl = nativeImpl;

    protected override (bool[], bool[]) GetColumnInternal(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      var intermediate = new sbyte[numRows];
      var nullsAsSbytes = new sbyte[numRows];
      _nativeImpl(table, columnIndex, intermediate, nullsAsSbytes, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var data = new bool[numRows];
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        data[i] = intermediate[i] != 0;
        nulls[i] = nullsAsSbytes[i] != 0;
      }
      return (data, nulls);
    }
  }

  public sealed class ForDateTime : ColumnFactoryForValueTypes<DateTime> {
    private readonly NativeImpl<Int64> _nativeImpl;

    public ForDateTime(NativeImpl<Int64> nativeImpl) => _nativeImpl = nativeImpl;

    protected override (DateTime[], bool[]) GetColumnInternal(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      var intermediate = new Int64[numRows];
      var nullsAsSbytes = new sbyte[numRows];
      _nativeImpl(table, columnIndex, intermediate, nullsAsSbytes, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var data = new DateTime[numRows];
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        var micros = intermediate[i] / 1000;
        data[i] = DateTimeOffset.UnixEpoch.AddMicroseconds(micros).DateTime;
        nulls[i] = nullsAsSbytes[i] != 0;
      }
      return (data, nulls);
    }
  }

  public sealed class ForOtherValueType<T> : ColumnFactoryForValueTypes<T> where T : struct {
    private readonly NativeImpl<T> _nativeImpl;

    public ForOtherValueType(NativeImpl<T> nativeImpl) => _nativeImpl = nativeImpl;

    protected override (T[], bool[]) GetColumnInternal(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      var data = new T[numRows];
      var nullsAsSbytes = new sbyte[numRows];
      _nativeImpl(table, columnIndex, data, nullsAsSbytes, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        nulls[i] = nullsAsSbytes[i] != 0;
      }
      return (data, nulls);
    }
  }
}
