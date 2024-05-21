using Deephaven.DeephavenClient.Interop;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Utility;

namespace Deephaven.DeephavenClient;

public sealed class TableHandle : IDisposable {
  internal NativePtr<NativeTableHandle> Self;
  public TableHandleManager Manager;
  public Schema Schema;
  public Int32 NumCols => Schema.NumCols;
  public Int64 NumRows => Schema.NumRows;
  public readonly bool IsStatic;

  internal TableHandle(NativePtr<NativeTableHandle> self, TableHandleManager manager) {
    Self = self;
    Manager = manager;

    NativeTableHandle.deephaven_client_TableHandle_GetAttributes(Self,
      out var numCols, out var numRows, out InteropBool isStatic, out var status1);
    status1.OkOrThrow();
    IsStatic = (bool)isStatic;

    var columnHandles = new StringHandle[numCols];
    var elementTypesAsInt = new Int32[numCols];
    NativeTableHandle.deephaven_client_TableHandle_GetSchema(self, numCols, columnHandles,
      elementTypesAsInt, out var stringPoolHandle, out var status2);
    status2.OkOrThrow();

    var pool = stringPoolHandle.ExportAndDestroy();
    var columnNames = columnHandles.Select(pool.Get).ToArray();
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

  public void AddTable(TableHandle tableToAdd) {
    NativeTableHandle.deephaven_client_TableHandle_AddTable(Self, tableToAdd.Self, out var status);
    status.OkOrThrow();
  }

  public void RemoveTable(TableHandle tableToRemove) {
    NativeTableHandle.deephaven_client_TableHandle_RemoveTable(Self, tableToRemove.Self, out var status);
    status.OkOrThrow();
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
    Manager.RemoveSubscription(handle);
    NativeTableHandle.deephaven_client_TableHandle_Unsubscribe(Self, handle.Self, out var status);
    status.OkOrThrow();
    handle.Dispose();
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
    status.OkOrThrow();
    return result;
  }

  public void Stream(TextWriter textWriter, bool wantHeaders) {
    var s = ToString(wantHeaders);
    textWriter.Write(s);
  }

  private void ReleaseUnmanagedResources() {
    if (!Self.TryRelease(out var old)) {
      return;
    }
    NativeTableHandle.deephaven_client_TableHandle_dtor(old);
  }
}

internal partial class NativeTableHandle {
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_dtor(NativePtr<NativeTableHandle> self);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_TableHandle_GetAttributes(
    NativePtr<NativeTableHandle> self,
    out Int32 numColumns, out Int64 numRows, out InteropBool isStatic,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_TableHandle_GetSchema(
    NativePtr<NativeTableHandle> self,
    Int32 numColumns,
    StringHandle[] columnHandles,
    Int32[] columnTypes,
    out StringPoolHandle stringPoolHandle,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Where(
    NativePtr<NativeTableHandle> self,
    string condition,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Select(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_SelectDistinct(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_View(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> clientTable,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_LastBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_WhereIn(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeTableHandle> filterTable,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_AddTable(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeTableHandle> tableToAdd,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_RemoveTable(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeTableHandle> tableToRemove,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_By(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeAggregateCombo> aggregateCombo,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_DropColumns(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> clientTable,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Update(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_LazyUpdate(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_BindToVariable(
    NativePtr<NativeTableHandle> self,
    string variable,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_ToString(
    NativePtr<NativeTableHandle> self,
    Int32 wantHeaders,
    out StringHandle resulHandle,
    out StringPoolHandle stringPoolHandle,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_ToArrowTable(
    NativePtr<NativeTableHandle> self,
    out NativePtr<NativeArrowTable> arrowTable,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_ToClientTable(
    NativePtr<NativeTableHandle> self,
    out NativePtr<NativeClientTable> clientTable,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Head(
    NativePtr<NativeTableHandle> self,
    Int64 numRows,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Tail(
    NativePtr<NativeTableHandle> self,
    Int64 numRows,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatusNew status);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  public delegate void NativeOnUpdate(NativePtr<NativeTickingUpdate> tickingUpdate);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  public delegate void NativeOnFailure(StringHandle errorHandle, StringPoolHandle stringPoolHandle);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Subscribe(
    NativePtr<NativeTableHandle> self,
    NativeOnUpdate nativeOnUpdate, NativeOnFailure nativeOnFailure,
    out NativePtr<NativeSubscriptionHandle> nativeSubscriptionHandle,
    out ErrorStatusNew status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Unsubscribe(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeSubscriptionHandle> nativeSubscriptionHandle,
    out ErrorStatusNew status);
}
