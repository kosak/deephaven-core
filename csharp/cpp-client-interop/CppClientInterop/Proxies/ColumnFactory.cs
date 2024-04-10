using CppClientInterop.CppClientInterop;
using Deephaven.CppClientInterop.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.CppClientInterop;

internal abstract class ColumnFactory<TableType> {
  public abstract Array GetColumn(NativePtr<TableType> table, Int64 numRows);

  public delegate void NativeImpl<in T>(NativePtr<TableType> table, Int32 columnIndex,
    T[] data, bool[]? optionalDestNullFlags, Int64 numRows, out ErrorStatus status);

  public sealed class ForGeneric<T> : ColumnFactory<TableType> {
    private readonly NativeImpl<T> _nativeImpl;

    public ForGeneric(NativeImpl<T> nativeImpl) => _nativeImpl = nativeImpl;

    public override Array GetColumn(NativePtr<TableType> table, Int32 columnIndex, Int64 numRows) {
      var result = new T[numRows];
      _nativeImpl(table, columnIndex, result, null, numRows, out var errorStatus);
      return errorStatus.Unwrap(result);
    }
  }

  public sealed class ForBool : ColumnFactory<TableType> {
    private readonly NativeImpl<byte> _nativeImpl;

    public ForBool(NativeImpl<byte> nativeImpl) => _nativeImpl = nativeImpl;

    public override Array GetColumn(NativePtr<TableType> table, Int64 numRows) {
      var intermediate = new byte[numRows];
      _nativeImpl(table, intermediate, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var result = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        result[i] = intermediate[i] != 0;
      }

      return result;
    }
  }

  public sealed class ForDateTime : ColumnFactory<TableType> {
    private readonly NativeImpl<Int64> _nativeImpl;

    public ForDateTime(NativeImpl<Int64> nativeImpl) => _nativeImpl = nativeImpl;

    public override Array GetColumn(NativePtr<TableType> table, Int64 numRows) {
      var intermediate = new Int64[numRows];
      _nativeImpl(table, intermediate, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var result = new DateTime[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        // TODO: probably support something with more precision than the .NET DateTime type
        var millis = intermediate[i] / 1000;
        result[i] = DateTimeOffset.FromUnixTimeMilliseconds(millis).DateTime;
      }

      return result;
    }
  }
}
