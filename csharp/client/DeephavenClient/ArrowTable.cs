using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Utility;

namespace Deephaven.DeephavenClient;

public class ArrowTable : IDisposable {
  internal NativePtr<NativeArrowTable> Self;
  public readonly Int32 NumColumns;
  public readonly Int64 NumRows;
  private readonly string[] _columnNames;
  private readonly ElementTypeId[] _columnElementTypes;

  internal ArrowTable(NativePtr<NativeArrowTable> self) {
    Self = self;
    NativeArrowTable.deephaven_client_ArrowTable_GetDimensions(self, out NumColumns, out NumRows, out var status1);
    status1.OkOrThrow();
    _columnNames = new string[NumColumns];
    _columnElementTypes = new ElementTypeId[NumColumns];

    var elementTypesAsInt = new Int32[NumColumns];
    NativeArrowTable.deephaven_client_ArrowTable_GetSchema(self, NumColumns, _columnNames, elementTypesAsInt, out var status2);
    status2.OkOrThrow();
    for (var i = 0; i != NumColumns; ++i) {
      _columnElementTypes[i] = (ElementTypeId)elementTypesAsInt[i];
    }
  }

  ~ArrowTable() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  public Array Column(Int32 index) {
    var factory = ArrowTableColumnFactory.Of(_columnElementTypes[index]);
    return factory.GetColumn(Self, index, NumRows);
  }

  public void ReleaseUnmanagedResources() {
    var temp = Self.Release();
    if (temp.IsNull) {
      return;
    }
    NativeArrowTable.deephaven_client_ArrowTable_dtor(temp);
  }
}

internal static class ArrowTableColumnFactory {
  private static readonly ColumnFactory<NativeArrowTable>[] Factories = {
    new ColumnFactory<NativeArrowTable>.ForGeneric<char>(NativeArrowTable.deephaven_client_ArrowTable_GetCharColumn),
    new ColumnFactory<NativeArrowTable>.ForGeneric<SByte>(NativeArrowTable.deephaven_client_ArrowTable_GetInt8Column),
    new ColumnFactory<NativeArrowTable>.ForGeneric<Int16>(NativeArrowTable.deephaven_client_ArrowTable_GetInt16Column),
    new ColumnFactory<NativeArrowTable>.ForGeneric<Int32>(NativeArrowTable.deephaven_client_ArrowTable_GetInt32Column),
    new ColumnFactory<NativeArrowTable>.ForGeneric<Int64>(NativeArrowTable.deephaven_client_ArrowTable_GetInt64Column),
    new ColumnFactory<NativeArrowTable>.ForGeneric<float>(NativeArrowTable.deephaven_client_ArrowTable_GetFloatColumn),
    new ColumnFactory<NativeArrowTable>.ForGeneric<double>(NativeArrowTable.deephaven_client_ArrowTable_GetDoubleColumn),
    new ColumnFactory<NativeArrowTable>.ForGeneric<bool>(NativeArrowTable.deephaven_client_ArrowTable_GetBooleanAsInt32Column),
    new ColumnFactory<NativeArrowTable>.ForGeneric<string>(NativeArrowTable.deephaven_client_ArrowTable_GetStringColumn),
    // TODO: probably support something with more precision than the .NET DateTime type
    new  ColumnFactory<NativeArrowTable>.ForDateTime(NativeArrowTable.deephaven_client_ArrowTable_GetDateTimeAsLongColumn),
    // List - TODO(kosak)
  };

  public static ColumnFactory<NativeArrowTable> Of(ElementTypeId typeId) {
    return Factories[(int)typeId];
  }
}

internal class NativeArrowTable {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_dtor(NativePtr<NativeArrowTable> self);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetDimensions(
    NativePtr<NativeArrowTable> self, out Int32 numColumns, out Int64 numRows, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetSchema(
    NativePtr<NativeArrowTable> self, Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] string[] columns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] Int32[] columnTypes,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetCharColumn(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] char[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetInt8Column(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] SByte[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetInt16Column(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Int16[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetInt32Column(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Int32[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetInt64Column(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Int64[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetFloatColumn(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] float[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetDoubleColumn(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] double[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetBooleanAsInt32Column(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] bool[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetStringColumn(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] string[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ArrowTable_GetDateTimeAsLongColumn(
    NativePtr<NativeArrowTable> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Int64[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] sbyte[]? nullFlags,
    Int64 numRows,
    out ErrorStatus status);
}
