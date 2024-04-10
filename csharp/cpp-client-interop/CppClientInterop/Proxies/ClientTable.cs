using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using CppClientInterop.CppClientInterop;
using Deephaven.CppClientInterop.Native;

namespace Deephaven.CppClientInterop;

internal abstract class ClientTableColumnFactory {
  private static readonly ColumnFactory[] _factories = {
    new GenericColumnFactory<char>(Native.ClientTable.deephaven_client_ClientTable_GetCharColumn),
    new GenericColumnFactory<SByte>(Native.ClientTable.deephaven_client_ClientTable_GetInt8Column),
    new GenericColumnFactory<Int16>(Native.ClientTable.deephaven_client_ClientTable_GetInt16Column),
    new GenericColumnFactory<Int32>(Native.ClientTable.deephaven_client_ClientTable_GetInt32Column),
    new GenericColumnFactory<Int64>(Native.ClientTable.deephaven_client_ClientTable_GetInt64Column),
    new GenericColumnFactory<float>(Native.ClientTable.deephaven_client_ClientTable_GetFloatColumn),
    new GenericColumnFactory<double>(Native.ClientTable.deephaven_client_ClientTable_GetDoubleColumn),
    new ColumnFactory.BoolColumnFactory(Native.ClientTable.deephaven_client_ClientTable_GetBoolAsByteColumn),
    new GenericColumnFactory<string>(Native.ClientTable.deephaven_client_ClientTable_GetStringColumn),
    new GenericColumnFactory<DateTime>(Native.ClientTable.deephaven_client_ClientTable_GetDateTimeAsLongColumn),
    // List - TODO(kosak)
  };

  public static ColumnFactory Of(ElementTypeId typeId) {
    return _factories[(int)typeId];
  }
}

public class ClientTable : IDisposable {

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
