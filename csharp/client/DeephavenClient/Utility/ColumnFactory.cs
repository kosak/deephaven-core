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
    T[] data, InteropBool[]? nullFlags, Int64 numRows, out ErrorStatusNew status);

  public sealed class ForString : ColumnFactory<TTableType> {
    public delegate void NativeImplString(NativePtr<TTableType> table, Int32 columnIndex,
      StringHandle[] data, InteropBool[]? nullFlags, Int64 numRows, out StringPoolHandle stringPoolHandle,
      out ErrorStatusNew status);

    private readonly NativeImplString _nativeImpl;

    public ForString(NativeImplString nativeImpl) => _nativeImpl = nativeImpl;

    public override (Array, bool[]) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      var handles = new StringHandle[numRows];
      var interopNulls = new InteropBool[numRows];
      _nativeImpl(table, columnIndex, handles, interopNulls, numRows, out var stringPoolHandle, out var errorStatus);
      errorStatus.OkOrThrow();
      var pool = stringPoolHandle.ExportAndDestroy();

      var data = new string[numRows];
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        data[i] = pool.Get(handles[i]);
        nulls[i] = (bool)interopNulls[i];
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

  public sealed class ForChar: ColumnFactoryForValueTypes<char> {
    private readonly NativeImpl<Int16> _nativeImpl;

    public ForChar(NativeImpl<Int16> nativeImpl) => _nativeImpl = nativeImpl;

    protected override (char[], bool[]) GetColumnInternal(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      var intermediate = new Int16[numRows];
      var interopNulls = new InteropBool[numRows];
      _nativeImpl(table, columnIndex, intermediate, interopNulls, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var data = new char[numRows];
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        data[i] = (char)intermediate[i];
        nulls[i] = (bool)interopNulls[i];
      }
      return (data, nulls);
    }
  }

  public sealed class ForBool : ColumnFactoryForValueTypes<bool> {
    private readonly NativeImpl<InteropBool> _nativeImpl;

    public ForBool(NativeImpl<InteropBool> nativeImpl) => _nativeImpl = nativeImpl;

    protected override (bool[], bool[]) GetColumnInternal(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      var intermediate = new InteropBool[numRows];
      var interopNulls = new InteropBool[numRows];
      _nativeImpl(table, columnIndex, intermediate, interopNulls, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var data = new bool[numRows];
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        data[i] = (bool)intermediate[i];
        nulls[i] = (bool)interopNulls[i];
      }
      return (data, nulls);
    }
  }

  public sealed class ForDateTime : ColumnFactoryForValueTypes<DhDateTime> {
    private readonly NativeImpl<Int64> _nativeImpl;

    public ForDateTime(NativeImpl<Int64> nativeImpl) => _nativeImpl = nativeImpl;

    protected override (DhDateTime[], bool[]) GetColumnInternal(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows) {
      var intermediate = new Int64[numRows];
      var interopNulls = new InteropBool[numRows];
      _nativeImpl(table, columnIndex, intermediate, interopNulls, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var data = new DhDateTime[numRows];
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        data[i] = new DhDateTime(intermediate[i]);
        nulls[i] = (bool)interopNulls[i];
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
      var interopNulls = new InteropBool[numRows];
      _nativeImpl(table, columnIndex, data, interopNulls, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var nulls = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        nulls[i] = (bool)interopNulls[i];
      }
      return (data, nulls);
    }
  }
}
