using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient;

public sealed class TableHandle : IDisposable {
  internal NativePtr<NativeTableHandle> Self;
  internal TableHandleManager Manager;

  internal TableHandle(NativePtr<NativeTableHandle> self, TableHandleManager manager) {
    Self = self;
    Manager = manager;
  }

  ~TableHandle() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  public TableHandle Update(params string[] columnSpecs) {
    NativeTableHandle.deephaven_client_TableHandle_Update(Self, columnSpecs, columnSpecs.Length,
      out var result, out var status);
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

  private void ReleaseUnmanagedResources() {
    var temp = Self.Reset();
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
  internal static extern void deephaven_client_TableHandle_GetManager(NativePtr<NativeTableHandle> self,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Select(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_View(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_DropColumns(NativePtr<NativeTableHandle> self,
    [In] string[] columns, Int32 numColumns, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_client_TableHandle_Update(NativePtr<NativeTableHandle> self,
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
