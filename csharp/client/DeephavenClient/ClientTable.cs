using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Utility;

namespace Deephaven.DeephavenClient;

public class ClientTable : IDisposable {
  internal NativePtr<NativeClientTable> Self;
  public readonly Schema Schema;

  internal ClientTable(NativePtr<NativeClientTable> self) {
    Self = self;
    NativeClientTable.deephaven_client_ClientTable_GetDimensions(self,
      out var numColumns, out var numRows, out var status1);
    status1.OkOrThrow();

    var columnNames = new string[numColumns];
    var elementTypesAsInt = new Int32[numColumns];
    NativeClientTable.deephaven_client_ClientTable_Schema(self, numColumns, columnNames, elementTypesAsInt,
      out var status2);
    status2.OkOrThrow();
    Schema = new Schema(columnNames, elementTypesAsInt, numRows);
  }

  ~ClientTable() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  public Array GetColumn(Int32 index) {
    var elementType = Schema.Types[index];
    var factory = ClientTableColumnFactory.Of(elementType);
    return factory.GetColumn(Self, index, Schema.NumRows);
  }

  private void ReleaseUnmanagedResources() {
    var temp = Self.Release();
    if (temp.IsNull) {
      return;
    }
    NativeClientTable.deephaven_client_ClientTable_dtor(temp);
  }
}

internal abstract class ClientTableColumnFactory {
  private static readonly ColumnFactory<NativeClientTable>[] Factories = {
    new ColumnFactory<NativeClientTable>.ForGeneric<char>(NativeClientTable.deephaven_client_ClientTableHelper_GetCharColumn),
    new ColumnFactory<NativeClientTable>.ForGeneric<SByte>(NativeClientTable.deephaven_client_ClientTableHelper_GetInt8Column),
    new ColumnFactory<NativeClientTable>.ForGeneric<Int16>(NativeClientTable.deephaven_client_ClientTableHelper_GetInt16Column),
    new ColumnFactory<NativeClientTable>.ForGeneric<Int32>(NativeClientTable.deephaven_client_ClientTableHelper_GetInt32Column),
    new ColumnFactory<NativeClientTable>.ForGeneric<Int64>(NativeClientTable.deephaven_client_ClientTableHelper_GetInt64Column),
    new ColumnFactory<NativeClientTable>.ForGeneric<float>(NativeClientTable.deephaven_client_ClientTableHelper_GetFloatColumn),
    new ColumnFactory<NativeClientTable>.ForGeneric<double>(NativeClientTable.deephaven_client_ClientTableHelper_GetDoubleColumn),
    new ColumnFactory<NativeClientTable>.ForGeneric<bool>(NativeClientTable.deephaven_client_ClientTableHelper_GetBooleanAsInt32Column),
    new ColumnFactory<NativeClientTable>.ForGeneric<string>(NativeClientTable.deephaven_client_ClientTableHelper_GetStringColumn),
    // TODO: probably support something with more precision than the .NET DateTime type
    new ColumnFactory<NativeClientTable>.ForDateTime(NativeClientTable.deephaven_client_ClientTableHelper_GetDateTimeAsLongColumn),
    // List - TODO(kosak)
  };

  public static ColumnFactory<NativeClientTable> Of(ElementTypeId typeId) {
    return Factories[(int)typeId];
  }
}

internal class NativeClientTable {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTable_dtor(NativePtr<NativeClientTable> self);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTable_GetDimensions(
    NativePtr<NativeClientTable> self, out Int32 numColumns, out Int64 numWRows, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTable_Schema(
    NativePtr<NativeClientTable> self, Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] string[] columns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] Int32[] columnTypes,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetCharColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] char[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetInt8Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] SByte[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetInt16Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Int16[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetInt32Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Int32[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetInt64Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Int64[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetFloatColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] float[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetDoubleColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] double[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetBooleanAsInt32Column(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[] data, // Note! Windows default marshaling for bool is int32
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetStringColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] string[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientTableHelper_GetDateTimeAsLongColumn(
    NativePtr<NativeClientTable> self,
    Int32 columnIndex,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Int64[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[]? optionalDestNullFlags,
    Int64 numRows,
    out ErrorStatus status);
}
