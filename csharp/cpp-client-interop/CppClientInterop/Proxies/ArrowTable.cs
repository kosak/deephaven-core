using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using Deephaven.CppClientInterop.Native;

namespace Deephaven.CppClientInterop;

// put this somewhere
internal enum ElementTypeId {
  Char,
  Int8,
  Int16,
  Int32,
  Int64,
  Float,
  Double,
  Bool,
  String,
  Timestamp,
  List
};

internal abstract class ColumnFactory {
  private static ColumnFactory[] _factories = { new GenericColumnFactory<char>(Native.ArrowTable.deephaven_client_ArrowTable_GetCharColumn),
    new GenericColumnFactory<SByte>(Native.ArrowTable.deephaven_client_ArrowTable_GetInt8Column),
    new GenericColumnFactory<Int16>(Native.ArrowTable.deephaven_client_ArrowTable_GetInt16Column),
    new GenericColumnFactory<Int32>(Native.ArrowTable.deephaven_client_ArrowTable_GetInt32Column),
    new GenericColumnFactory<Int64>(Native.ArrowTable.deephaven_client_ArrowTable_GetInt64Column),
    new GenericColumnFactory<float>(Native.ArrowTable.deephaven_client_ArrowTable_GetFloatColumn),
    new GenericColumnFactory<double>(Native.ArrowTable.deephaven_client_ArrowTable_GetDoubleColumn),
    new BoolColumnFactory(),
    new GenericColumnFactory<string>(Native.ArrowTable.deephaven_client_ArrowTable_GetStringColumn),
    // Timestamp - TODO(kosak)
    // List - TODO(kosak)
  };

  public static ColumnFactory Of(ElementTypeId typeId) {
    return _factories[(int)typeId];
  }

  public abstract Array GetColumn(NativePtr<Native.ArrowTable> self, Int64 numRows);

  private sealed class GenericColumnFactory<T> : ColumnFactory {
    public delegate void NativeMethod(NativePtr<Native.ArrowTable> self, T[] data, Int64 numRows,
      out ErrorStatus status);

    private readonly NativeMethod _nativeMethod;

    public GenericColumnFactory(NativeMethod nativeMethod) => _nativeMethod = nativeMethod;

    public override Array GetColumn(NativePtr<Native.ArrowTable> table, Int64 numRows) {
      var result = new T[numRows];
      _nativeMethod(table, result, numRows, out var errorStatus);
      return errorStatus.Unwrap(result);
    }
  }

  private sealed class BoolColumnFactory : ColumnFactory {
    public override Array GetColumn(NativePtr<Native.ArrowTable> table, Int64 numRows) {
      var intermediate = new byte[numRows];
      Native.ArrowTable.deephaven_client_ArrowTable_GetBoolAsByteColumn(table, intermediate, numRows,
        out var errorStatus);
      errorStatus.OkOrThrow();
      var result = new bool[numRows];
      for (Int64 i = 0; i < numRows; ++i) {
        result[i] = intermediate[i] != 0;
      }

      return result;
    }
  }
}

public class ArrowTable : IDisposable {

  internal NativePtr<Native.ArrowTable> self;
  private readonly Int32 numColumns;
  private readonly Int64 numRows;
  private readonly string[] columnNames;
  private readonly ElementTypeId[] columnElementTypes;

  internal ArrowTable(NativePtr<Native.ArrowTable> self, Int32 numColumns, Int64 numRows) {
    this.self = self;
    this.numColumns = numColumns;
    this.numRows = numRows;
    columnNames = new string[numColumns];
    columnElementTypes = new ElementTypeId[numColumns];

    var elementTypesAsInt = new Int32[numColumns];
    Native.ArrowTable.deephaven_client_ArrowTable_GetSchema(self, numColumns, columnNames, elementTypesAsInt, out var status);
    status.OkOrThrow();
    for (var i = 0; i != numColumns; ++i) {
      columnElementTypes[i] = (ElementTypeId)elementTypesAsInt[i];
    }
  }

  ~ArrowTable() {
    Dispose();
  }

  public void Dispose() {
    if (self.ptr == IntPtr.Zero) {
      return;
    }

    var temp = self;  // paranoia
    self.ptr = IntPtr.Zero;
    GC.SuppressFinalize(this);

    Native.ArrowTable.deephaven_client_ArrowTable_dtor(temp);
  }

  public Array Column(Int32 index) {
    var factory = ColumnFactory.Of(columnElementTypes[index]);
    return factory.GetColumn(self, numRows);
  }
}
