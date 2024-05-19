using Deephaven.DeephavenClient.Interop;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Utility;

namespace Deephaven.DeephavenClient;

public sealed class TableHandle : IDisposable {
  internal NativePtr<NativeTableHandle> Self;
  public TableHandleManager Manager;
  public Schema Schema;
  public readonly Int32 NumRows;
  public readonly Int64 NumCols;
  public readonly bool IsStatic;

  internal TableHandle(NativePtr<NativeTableHandle> self, TableHandleManager manager) {
    Self = self;
    Manager = manager;

    NativeTableHandle.deephaven_client_TableHandle_GetAttributes(Self,
      out var numColumns, out var numRows, out InteropBool isStatic, out var status1);
    status1.OkOrThrow();

    var columnNames = new string[numColumns];
    var elementTypesAsInt = new Int32[numColumns];
    NativeTableHandle.deephaven_client_TableHandle_GetSchema(self, numColumns, columnNames,
      elementTypesAsInt, out var status2);
    status2.OkOrThrow();
    Schema = new Schema(columnNames, elementTypesAsInt, numRows);
  }

  ~TableHandle() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  public TableHandle Where(string condition) {
    NativeTableHandle.deephaven_client_TableHandle_Where(Self, condition, out var result,
      out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle Select(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_Select(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle View(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_View(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle DropColumns(params string[] columns) {
    NativeTableHandle.deephaven_client_TableHandle_DropColumns(Self,
      columns, columns.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle Update(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_Update(Self, columnSpecs, columnSpecs.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle LazyUpdate(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_LazyUpdate(Self, columnSpecs, columnSpecs.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle SelectDistinct(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_SelectDistinct(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle Head(Int64 numRows) {
    NativeTableHandle.deephaven_client_TableHandle_Head(Self, numRows,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle Tail(Int64 numRows) {
    NativeTableHandle.deephaven_client_TableHandle_Tail(Self, numRows,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle LastBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_LastBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle WhereIn(TableHandle filterTable, params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_WhereIn(Self,
      filterTable.Self, columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle By(AggregateCombo combo, params string[] groupByColumns) {
    using var comboInternal = combo.Invoke();
    NativeTableHandle.deephaven_client_TableHandle_By(Self,
      comboInternal.Self, groupByColumns, groupByColumns.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public void BindToVariable(string variable) {
    NativeTableHandle.deephaven_client_TableHandle_BindToVariable(Self, variable, out var status);
    status.OkOrThrow();
  }

  private class TickingWrapper {
    private readonly ITickingCallback _callback;

    public TickingWrapper(ITickingCallback callback) => this._callback = callback;

    public void NativeOnUpdate(NativePtr<NativeTickingUpdate> nativeTickingUpdate) {
      using var tickingUpdate = new TickingUpdate(nativeTickingUpdate);
      _callback.OnTick(tickingUpdate);
    }
  }

  public SubscriptionHandle Subscribe(ITickingCallback callback) {
    var tw = new TickingWrapper(callback);
    NativeTableHandle.deephaven_client_TableHandle_Subscribe(Self, tw.NativeOnUpdate, callback.OnFailure,
      out var nativeSusbcriptionHandle, out var status);
    status.OkOrThrow();
    var result = new SubscriptionHandle(nativeSusbcriptionHandle);
    Manager.AddSubscription(result, tw);
    return result;
  }

  public void Unsubscribe(SubscriptionHandle handle) {
    if (handle.Self.ptr == IntPtr.Zero) {
      return;
    }
    var nativePtr = handle.Self;
    handle.Self.ptr = IntPtr.Zero;
    Manager.RemoveSubscription(handle);
    NativeTableHandle.deephaven_client_TableHandle_Unsubscribe(Self, nativePtr, out var status);
    NativeSubscriptionHandle.deephaven_client_SubscriptionHandle_dtor(nativePtr);
    status.OkOrThrow();
  }

  public ArrowTable ToArrowTable() {
    NativeTableHandle.deephaven_client_TableHandle_ToArrowTable(Self, out var arrowTable, out var status);
    status.OkOrThrow();
    return new ArrowTable(arrowTable);
  }

  public ClientTable ToClientTable() {
    NativeTableHandle.deephaven_client_TableHandle_ToClientTable(Self, out var clientTable, out var status);
    status.OkOrThrow();
    return new ClientTable(clientTable);
  }

  public string ToString(bool wantHeaders) {
    NativeTableHandle.deephaven_client_TableHandle_ToString(Self, wantHeaders ? 1 : 0, out var result,
      out var status);
    return status.Unwrap(result);
  }

  public void Stream(TextWriter textWriter, bool wantHeaders) {
    var s = ToString(wantHeaders);
    textWriter.Write(s);
  }

  private void ReleaseUnmanagedResources() {
    var temp = Self.Release();
    if (temp.IsNull) {
      return;
    }
    NativeTableHandle.deephaven_client_TableHandle_dtor(temp);
  }
}

internal class NativeTableHandle {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_dtor(NativePtr<NativeTableHandle> self);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_TableHandle_GetAttributes(
    NativePtr<NativeTableHandle> self, out Int32 numColumns, out Int64 numRows,
    out InteropBool isStatic, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_TableHandle_GetSchema(
    NativePtr<NativeTableHandle> self,
    Int32 numColumns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] string[] columns,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] Int32[] columnTypes,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Where(NativePtr<NativeTableHandle> self,
    string condition, out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Select(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_SelectDistinct(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_View(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeTableHandle> clientTable,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_LastBy(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_WhereIn(NativePtr<NativeTableHandle> self,
    NativePtr<NativeTableHandle> filterTable,
    [In] string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_By(NativePtr<NativeTableHandle> self,
    NativePtr<NativeAggregateCombo> aggregateCombo,
    [In] string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_DropColumns(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeTableHandle> clientTable,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Update(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeTableHandle> result, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_LazyUpdate(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeTableHandle> result, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_BindToVariable(NativePtr<NativeTableHandle> self,
    string variable, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
  internal static extern void deephaven_client_TableHandle_ToString(NativePtr<NativeTableHandle> self,
    Int32 wantHeaders, out string result, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_ToArrowTable(NativePtr<NativeTableHandle> self,
    out NativePtr<NativeArrowTable> arrowTable, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_ToClientTable(NativePtr<NativeTableHandle> self,
    out NativePtr<NativeClientTable> clientTable, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Head(NativePtr<NativeTableHandle> self,
    Int64 numRows, out NativePtr<NativeTableHandle> result, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Tail(NativePtr<NativeTableHandle> self,
    Int64 numRows, out NativePtr<NativeTableHandle> result, out ErrorStatus status);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  public delegate void NativeOnUpdate(NativePtr<NativeTickingUpdate> tickingUpdate);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  public delegate void NativeOnFailure(string error);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Subscribe(NativePtr<NativeTableHandle> self,
    NativeOnUpdate nativeOnUpdate, NativeOnFailure nativeOnFailure,
    out NativePtr<NativeSubscriptionHandle> nativeSubscriptionHandle, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Unsubscribe(NativePtr<NativeTableHandle> self,
    NativePtr<NativeSubscriptionHandle> nativeSubscriptionHandle, out ErrorStatus status);
}
