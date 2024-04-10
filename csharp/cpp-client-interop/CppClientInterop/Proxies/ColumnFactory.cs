using CppClientInterop.CppClientInterop;
using Deephaven.CppClientInterop.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.CppClientInterop;

internal abstract class ColumnFactory {
  public abstract Array GetColumn(NativePtr<Native.ArrowTable> self, Int64 numRows);

  public delegate void NativeImpl<in T>(NativePtr<Native.ArrowTable> self, T[] data, Int64 numRows,
    out ErrorStatus status);

  private sealed class GenericColumnFactory<T> : ColumnFactory {
    private readonly NativeImpl<T> _nativeImpl;

    public GenericColumnFactory(NativeImpl<T> nativeImpl) => _nativeImpl = nativeImpl;

    public override Array GetColumn(NativePtr<Native.ArrowTable> table, Int64 numRows) {
      var result = new T[numRows];
      _nativeImpl(table, result, numRows, out var errorStatus);
      return errorStatus.Unwrap(result);
    }
  }

  public sealed class BoolColumnFactory : ColumnFactory {
    private readonly NativeImpl<byte> _nativeImpl;

    public BoolColumnFactory(NativeImpl<byte> nativeImpl) => _nativeImpl = nativeImpl;

    public override Array GetColumn(NativePtr<Native.ArrowTable> table, Int64 numRows) {
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
}
