using Deephaven.DeephavenClient.Interop;
using System;

namespace Deephaven.DeephavenClient.Utility;

internal enum ColumnFactoryMode {
  DataOnly,
  DataAndNullArray,
  ArrayOfNullables
}

internal abstract class ColumnFactory<TTableType> {

  public abstract (Array, bool[]?) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
    Int64 numRows, ColumnFactoryMode mode);

  public delegate void NativeImpl<in T>(NativePtr<TTableType> table, Int32 columnIndex,
    T[] data, sbyte[]? nullFlags, Int64 numRows, out ErrorStatus status);

  public sealed class ForGeneric<T> : ColumnFactory<TTableType> {
    private readonly NativeImpl<T> _nativeImpl;

    public ForGeneric(NativeImpl<T> nativeImpl) => _nativeImpl = nativeImpl;

    public override (Array, bool[]?) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows, ColumnFactoryMode mode) {
      var data = new T[numRows];
      var nullsAsSbytes = mode == ColumnFactoryMode.DataOnly ? null : new sbyte[numRows];
      _nativeImpl(table, columnIndex, data, nullsAsSbytes, numRows, out var errorStatus);
      errorStatus.OkOrThrow();

      return Adapt(data, nullsAsSbytes, mode);
    }
  }

  public sealed class ForBool : ColumnFactory<TTableType> {
    private readonly NativeImpl<sbyte> _nativeImpl;

    public ForBool(NativeImpl<sbyte> nativeImpl) => _nativeImpl = nativeImpl;

    public override (Array, bool[]?) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows, ColumnFactoryMode mode) {
      var intermediate = new sbyte[numRows];
      var nullsAsSbytes = mode == ColumnFactoryMode.DataOnly ? null : new sbyte[numRows];
      _nativeImpl(table, columnIndex, intermediate, nullsAsSbytes, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var data = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        data[i] = intermediate[i] != 0;
      }
      return Adapt(data, nullsAsSbytes, mode);
    }
  }

  public sealed class ForDateTime : ColumnFactory<TTableType> {
    private readonly NativeImpl<Int64> _nativeImpl;

    public ForDateTime(NativeImpl<Int64> nativeImpl) => _nativeImpl = nativeImpl;

    public override (Array, bool[]?) GetColumn(NativePtr<TTableType> table, Int32 columnIndex,
      Int64 numRows, ColumnFactoryMode mode) {
      var intermediate = new Int64[numRows];
      var nullsAsSbytes = mode == ColumnFactoryMode.DataOnly ? null : new sbyte[numRows];
      _nativeImpl(table, columnIndex, intermediate, nullsAsSbytes, numRows, out var errorStatus);
      errorStatus.OkOrThrow();
      var data = new DateTime[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        var micros = intermediate[i] / 1000;
        data[i] = DateTimeOffset.UnixEpoch.AddMicroseconds(micros).DateTime;
      }
      return Adapt(data, nullsAsSbytes, mode);
    }
  }

  private static (Array, bool[]?) Adapt<T>(T[] data, sbyte[]? nullsAsSbytes, ColumnFactoryMode mode) {
    if (mode == ColumnFactoryMode.DataOnly) {
      return (data, null);
    }

    var numRows = data.Length;

    if (mode == ColumnFactoryMode.DataAndNullArray) {
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
