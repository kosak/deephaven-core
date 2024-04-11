using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using CppClientInterop.CppClientInterop;
using Deephaven.CppClientInterop.Native;

namespace Deephaven.CppClientInterop;

internal abstract class ClientTableColumnFactory {
  private static readonly ColumnFactory<Native.ClientTable>[] _factories = {
    new ColumnFactory<Native.ClientTable>.ForGeneric<char>(Native.ClientTable.deephaven_client_ClientTableHelper_GetCharColumn),
    new ColumnFactory<Native.ClientTable>.ForGeneric<SByte>(Native.ClientTable.deephaven_client_ClientTableHelper_GetInt8Column),
    new ColumnFactory<Native.ClientTable>.ForGeneric<Int16>(Native.ClientTable.deephaven_client_ClientTableHelper_GetInt16Column),
    new ColumnFactory<Native.ClientTable>.ForGeneric<Int32>(Native.ClientTable.deephaven_client_ClientTableHelper_GetInt32Column),
    new ColumnFactory<Native.ClientTable>.ForGeneric<Int64>(Native.ClientTable.deephaven_client_ClientTableHelper_GetInt64Column),
    new ColumnFactory<Native.ClientTable>.ForGeneric<float>(Native.ClientTable.deephaven_client_ClientTableHelper_GetFloatColumn),
    new ColumnFactory<Native.ClientTable>.ForGeneric<double>(Native.ClientTable.deephaven_client_ClientTableHelper_GetDoubleColumn),
    new ColumnFactory<Native.ClientTable>.ForGeneric<bool>(Native.ClientTable.deephaven_client_ClientTableHelper_GetBooleanAsInt32Column),
    new ColumnFactory<Native.ClientTable>.ForGeneric<string>(Native.ClientTable.deephaven_client_ClientTableHelper_GetStringColumn),
    // TODO: probably support something with more precision than the .NET DateTime type
    new ColumnFactory<Native.ClientTable>.ForDateTime(Native.ClientTable.deephaven_client_ClientTableHelper_GetDateTimeAsLongColumn),
    // List - TODO(kosak)
  };

  public static ColumnFactory<Native.ClientTable> Of(ElementTypeId typeId) {
    return _factories[(int)typeId];
  }
}

public class ClientTable : IDisposable {
  internal NativePtr<Native.ClientTable> self;
  public Int32 NumColumns { get; }
  public Int64 NumRows { get; }
  public string[] ColumnNames;
  private readonly ElementTypeId[] columnElementTypes;

  internal ClientTable(NativePtr<Native.ClientTable> self, Int32 numColumns, Int64 numRows) {
    this.self = self;
    NumColumns = numColumns;
    NumRows = numRows;
    ColumnNames = new string[numColumns];
    columnElementTypes = new ElementTypeId[numColumns];

    var elementTypesAsInt = new Int32[numColumns];
    Native.ClientTable.deephaven_client_ClientTable_Schema(self, numColumns, ColumnNames, elementTypesAsInt, out var status);
    status.OkOrThrow();
    for (var i = 0; i != numColumns; ++i) {
      columnElementTypes[i] = (ElementTypeId)elementTypesAsInt[i];
    }
  }

  ~ClientTable() {
    Dispose();
  }

  public void Dispose() {
    if (self.ptr == IntPtr.Zero) {
      return;
    }

    var temp = self;  // paranoia
    self.ptr = IntPtr.Zero;
    GC.SuppressFinalize(this);

    Native.ClientTable.deephaven_client_ClientTable_dtor(temp);
  }

  public Array GetColumn(Int32 index) {
    var factory = ClientTableColumnFactory.Of(columnElementTypes[index]);
    return factory.GetColumn(self, index, NumRows);
  }
}
