using Deephaven.DeephavenClient.Interop;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Utility;

namespace Deephaven.DeephavenClient;

public class ClientTable : IDisposable {
  internal NativePtr<NativeClientTable> Self;
  public readonly Schema Schema;

  public Int32 NumCols => Schema.NumCols;

  internal ClientTable(NativePtr<NativeClientTable> self) {
    Self = self;
    NativeClientTable.deephaven_client_ClientTable_GetDimensions(Self,
      out var numColumns, out var numRows, out var status1);
    status1.OkOrThrow();

    var columnNameHandles = new StringHandle[numColumns];
    var elementTypesAsInt = new Int32[numColumns];
    NativeClientTable.deephaven_client_ClientTable_Schema(self, numColumns, columnNameHandles, elementTypesAsInt,
      out var stringPoolHandle, out var status2);
    status2.OkOrThrow();
    var pool = stringPoolHandle.ExportAndDestroy();

    var columnNames = columnNameHandles.Select(pool.Get).ToArray();
    Schema = new Schema(columnNames, elementTypesAsInt, numRows);
  }

  ~ClientTable() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  public (Array, bool[]) GetColumn(Int32 index) {
    var elementType = Schema.Types[index];
    var factory = ClientTableColumnFactory.Of(elementType);
    var (data, nulls) = factory.GetColumn(Self, index, Schema.NumRows);
    return (data, nulls);
  }

  public Array GetNullableColumn(Int32 index) {
    var elementType = Schema.Types[index];
    var factory = ClientTableColumnFactory.Of(elementType);
    return factory.GetNullableColumn(Self, index, Schema.NumRows);
  }

  private void ReleaseUnmanagedResources() {
    if (!NativePtrUtil.TryRelease(ref Self, out var old)) {
      return;
    }
    NativeClientTable.deephaven_client_ClientTable_dtor(old);
  }
}

internal abstract class ClientTableColumnFactory {
  private static readonly ColumnFactory<NativeClientTable>[] Factories = {
    new ColumnFactory<NativeClientTable>.ForChar(NativeClientTable.deephaven_client_ClientTableHelper_GetCharAsInt16Column),
    new ColumnFactory<NativeClientTable>.ForOtherValueType<SByte>(NativeClientTable.deephaven_client_ClientTableHelper_GetInt8Column),
    new ColumnFactory<NativeClientTable>.ForOtherValueType<Int16>(NativeClientTable.deephaven_client_ClientTableHelper_GetInt16Column),
    new ColumnFactory<NativeClientTable>.ForOtherValueType<Int32>(NativeClientTable.deephaven_client_ClientTableHelper_GetInt32Column),
    new ColumnFactory<NativeClientTable>.ForOtherValueType<Int64>(NativeClientTable.deephaven_client_ClientTableHelper_GetInt64Column),
    new ColumnFactory<NativeClientTable>.ForOtherValueType<float>(NativeClientTable.deephaven_client_ClientTableHelper_GetFloatColumn),
    new ColumnFactory<NativeClientTable>.ForOtherValueType<double>(NativeClientTable.deephaven_client_ClientTableHelper_GetDoubleColumn),
    new ColumnFactory<NativeClientTable>.ForBool(NativeClientTable.deephaven_client_ClientTableHelper_GetBooleanAsInteropBoolColumn),
    new ColumnFactory<NativeClientTable>.ForString(NativeClientTable.deephaven_client_ClientTableHelper_GetStringColumn),
    new ColumnFactory<NativeClientTable>.ForDateTime(NativeClientTable.deephaven_client_ClientTableHelper_GetDateTimeAsInt64Column),
    // List - TODO(kosak)
  };

  public static ColumnFactory<NativeClientTable> Of(ElementTypeId typeId) {
    return Factories[(int)typeId];
  }
}

internal partial class NativeClientTable {
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTable_dtor(NativePtr<NativeClientTable> self);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTable_GetDimensions(
    NativePtr<NativeClientTable> self, out Int32 numColumns, out Int64 numWRows, out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTable_Schema(
    NativePtr<NativeClientTable> self,
    Int32 numColumns,
    StringHandle[] columnHandles,
    Int32[] columnTypes,
    out StringPoolHandle stringPool,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetInt8Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    sbyte[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetInt16Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    Int16[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetInt32Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    Int32[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetInt64Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    Int64[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetFloatColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    float[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetDoubleColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    double[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetBooleanAsInteropBoolColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    InteropBool[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetCharAsInt16Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    Int16[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetStringColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    StringHandle[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out StringPoolHandle stringPoolHandle,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_ClientTableHelper_GetDateTimeAsInt64Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    Int64[] data,
    InteropBool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatusNew status);
}
