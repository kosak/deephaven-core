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

  public TableHandle CrossJoin(TableHandle rightSide, string[] columnsToMatch,
    string[]? columnsToAdd = null) =>
    JoinHelper(rightSide, columnsToMatch, columnsToAdd,
      NativeTableHandle.deephaven_client_TableHandle_CrossJoin);

  public TableHandle NaturalJoin(TableHandle rightSide, string[] columnsToMatch,
    string[]? columnsToAdd = null) {
    columnsToAdd ??= Array.Empty<string>();
    NativeTableHandle.deephaven_client_TableHandle_NaturalJoin(Self,
      rightSide.Self, columnsToMatch, columnsToMatch.Length,
      columnsToAdd, columnsToAdd.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle LeftOuterJoin(TableHandle rightSide, string[] columnsToMatch,
    string[]? columnsToAdd = null) {
    columnsToAdd ??= Array.Empty<string>();
    NativeTableHandle.deephaven_client_TableHandle_LeftOuterJoin(Self,
      rightSide.Self, columnsToMatch, columnsToMatch.Length,
      columnsToAdd, columnsToAdd.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }


  public TableHandle ExactJoin(TableHandle rightSide, string[] columnsToMatch,
    string[]? columnsToAdd = null) {
    columnsToAdd ??= Array.Empty<string>();
    NativeTableHandle.deephaven_client_TableHandle_ExactJoin(Self,
      rightSide.Self, columnsToMatch, columnsToMatch.Length,
      columnsToAdd, columnsToAdd.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle Aj(TableHandle rightSide, string[] columnsToMatch,
    string[]? columnsToAdd = null) {
    columnsToAdd ??= Array.Empty<string>();
    NativeTableHandle.deephaven_client_TableHandle_Aj(Self,
      rightSide.Self, on, joins, joins.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle Raj(TableHandle rightSide, string[] columnsToMatch,
    string[]? columnsToAdd = null) {
    columnsToAdd ??= Array.Empty<string>();
    NativeTableHandle.deephaven_client_TableHandle_Raj(Self,
      rightSide.Self, on, joins, joins.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  private delegate void NativeJoinInvoker();

  private TableHandle JoinHelper(TableHandle rightSide, string[] columnsToMatch,
    string[]? columnsToAdd, NativeJoinInvoker invoker) { 
    columnsToAdd ??= Array.Empty<string>();
    invoker(rightSide.Self, columnsToMatch, columnsToMatch.Length,
      columnsToAdd, columnsToAdd.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle MinBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_MinBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle MaxBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_MaxBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle SumBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_SumBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle AbsSumBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_AbsSumBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle VarBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_VarBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle StdBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_StdBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle AvgBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_AvgBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle FirstBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_FirstBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle LastBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_LastBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle MedianBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_MedianBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle PercentileBy(double percentile, bool avgMedian, params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_PercentileBy(Self,
      percentile, (InteropBool)avgMedian, columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle PercentileBy(double percentile, params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_PercentileBy(Self,
      percentile, columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle CountBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_CountBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle WAvgBy(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_WAvgBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle TailBy(Int64 n, params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_TailBy(Self,
      columnSpecs, columnSpecs.Length, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public TableHandle HeadBy(Int64 n, params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_HeadBy(Self,
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
    NativeTableHandle.deephaven_client_TableHandle_ToString(Self, (InteropBool)wantHeaders, out var resultHandle,
      out var stringPoolHandle, out var status);
    status.OkOrThrow();
    return stringPoolHandle.ExportAndDestroy().Get(resultHandle);
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
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_TableHandle_GetSchema(
    NativePtr<NativeTableHandle> self,
    Int32 numColumns,
    StringHandle[] columnHandles,
    Int32[] columnTypes,
    out StringPoolHandle stringPoolHandle,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Where(
    NativePtr<NativeTableHandle> self,
    string condition,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Select(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_SelectDistinct(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_View(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> clientTable,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_MinBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_MaxBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_SumBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_AbsSumBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_VarBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_StdBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_AvgBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_FirstBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_LastBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_MedianBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_PercentileBy(
    NativePtr<NativeTableHandle> self,
    double percentile, InteropBool avgMedian,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_PercentileBy(
    NativePtr<NativeTableHandle> self,
    double percentile,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_CountBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_WAvgBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_TailBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_HeadBy(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_WhereIn(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeTableHandle> filterTable,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_AddTable(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeTableHandle> tableToAdd,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_RemoveTable(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeTableHandle> tableToRemove,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_By(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeAggregateCombo> aggregateCombo,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_DropColumns(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> clientTable,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Update(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_LazyUpdate(
    NativePtr<NativeTableHandle> self,
    string[] columns, Int32 numColumns,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_BindToVariable(
    NativePtr<NativeTableHandle> self,
    string variable,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_ToString(
    NativePtr<NativeTableHandle> self,
    InteropBool wantHeaders,
    out StringHandle resulHandle,
    out StringPoolHandle stringPoolHandle,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_ToArrowTable(
    NativePtr<NativeTableHandle> self,
    out NativePtr<NativeArrowTable> arrowTable,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_ToClientTable(
    NativePtr<NativeTableHandle> self,
    out NativePtr<NativeClientTable> clientTable,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Head(
    NativePtr<NativeTableHandle> self,
    Int64 numRows,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Tail(
    NativePtr<NativeTableHandle> self,
    Int64 numRows,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  public delegate void NativeOnUpdate(NativePtr<NativeTickingUpdate> tickingUpdate);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  public delegate void NativeOnFailure(StringHandle errorHandle, StringPoolHandle stringPoolHandle);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Subscribe(
    NativePtr<NativeTableHandle> self,
    NativeOnUpdate nativeOnUpdate, NativeOnFailure nativeOnFailure,
    out NativePtr<NativeSubscriptionHandle> nativeSubscriptionHandle,
    out ErrorStatus status);

  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  internal static partial void deephaven_client_TableHandle_Unsubscribe(
    NativePtr<NativeTableHandle> self,
    NativePtr<NativeSubscriptionHandle> nativeSubscriptionHandle,
    out ErrorStatus status);
}
