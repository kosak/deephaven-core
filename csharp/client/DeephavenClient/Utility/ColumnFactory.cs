using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Deephaven.DeephavenClient.Utility;

internal abstract class ColumnFactory<TTableType> {
  public enum Mode { DataOnly, DataAndNullArray, ArrayOfNullables }

  public abstract (Array, bool[]?) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
    Int64 numRows, Mode mode);

  public delegate void NativeImpl<in T>(NativePtr<TTableType> table, Int32 columnIndex,
    T[] data, sbyte[]? nullFlags, Int64 numRows, out ErrorStatus status);

  public sealed class ForGeneric<T> : ColumnFactory<TTableType> {
    private readonly NativeImpl<T> _nativeImpl;

    public ForGeneric(NativeImpl<T> nativeImpl) => _nativeImpl = nativeImpl;

    public override (Array, bool[]?) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows, Mode mode) {
      var data = new T[numRows];
      var nullsAsSbytes = mode == Mode.DataOnly ? null : new sbyte[numRows];
      _nativeImpl(table, columnIndex, data, nullsAsSbytes, numRows, out var errorStatus);
      errorStatus.OkOrThrow();

      return Adapt(data, nullsAsSbytes, mode);
    }

    if (mode == Mode.DataOnly) {
        return (data, null);
      }

      if (mode == Mode.DataAndNullArray) {
        var nulls = new bool[numRows];
        for (Int64 i = 0; i != numRows; ++i) {
          nulls[i] = nullsAsSbytes![i] != 0;
        }
        return (data, nulls);
      }

      // mode == Mode.ArrayOfNullables
      var nullableData = new T?[numRows];
      for (Int64 i = 0; i != numRows; ++i) {
        if (nullsAsSbytes![i] != 0) {
          nullableData[i] = data[i];
        }
      }

      return (nullableData, null);
    }
  }

  public sealed class ForBool : ColumnFactory<TTableType> {
    private readonly NativeImpl<sbyte> _nativeImpl;

    public ForBool(NativeImpl<sbyte> nativeImpl) => _nativeImpl = nativeImpl;

    public override Array GetColumn(NativePtr<TTableType> table, Int32 columnIndex, Int64 numRows) {
      var intermediate = new sbyte[numRows];
      _nativeImpl(table, columnIndex, intermediate, null, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var result = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        result[i] = intermediate[i] != 0;
      }
      return result;
    }
  }

  public sealed class ForDateTime : ColumnFactory<TTableType> {
    private readonly NativeImpl<Int64> _nativeImpl;

    public ForDateTime(NativeImpl<Int64> nativeImpl) => _nativeImpl = nativeImpl;

    public override Array GetColumn(NativePtr<TTableType> table, Int32 columnIndex, Int64 numRows) {
      var intermediate = new Int64[numRows];
      _nativeImpl(table, columnIndex, intermediate, null, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var result = new DateTime[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        var micros = intermediate[i] / 1000;
        result[i] = new DateTime(1970, 1, 1).AddMicroseconds(micros);
      }

      return result;
    }
  }
}
