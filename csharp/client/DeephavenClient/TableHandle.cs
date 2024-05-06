using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.DeephavenClient;

public sealed class TableHandle : IDisposable {
  internal NativePtr<NativeTableHandle> _self;
  internal TableHandleManager Manager;

  internal TableHandle(NativePtr<NativeTableHandle> self, TableHandleManager manager) {
    this._self = self;
    Manager = manager;
  }

  ~TableHandle() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  //
  // ~TableHandle() {
  //   Dispose();
  // }
  //
  // public void Dispose() {
  //   if (self.ptr == IntPtr.Zero) {
  //     return;
  //   }
  //   Native.TableHandle.deephaven_client_TableHandle_dtor(self);
  //   self.ptr = IntPtr.Zero;
  //   GC.SuppressFinalize(this);
  // }

  public TableHandle Update(params string[] columnSpecs) {
    deephaven_client_TableHandle_Update(_self, columnSpecs, columnSpecs.Length,
      out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, Manager);
  }

  public void BindToVariable(string variable) {
    deephaven_client_TableHandle_BindToVariable(_self, variable, out var status);
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
    deephaven_client_TableHandle_Subscribe(_self, tw.NativeOnUpdate, callback.OnFailure,
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
    deephaven_client_TableHandle_Unsubscribe(self, nativePtr, out var status);
    Native.SubscriptionHandle.deephaven_client_SubscriptionHandle_dtor(nativePtr);
    status.OkOrThrow();
  }

  public ArrowTable ToArrowTable() {
    Native.TableHandle.deephaven_client_TableHandle_ToArrowTable(self, out var arrowTable, out var status);
    status.OkOrThrow();
    return new ArrowTable(arrowTable);
  }

  public ClientTable ToClientTable() {
    Native.TableHandle.deephaven_client_TableHandle_ToClientTable(self, out var clientTable, out var status);
    status.OkOrThrow();
    return new ClientTable(clientTable);
  }

  public string ToString(bool wantHeaders) {
    Native.TableHandle.deephaven_client_TableHandle_ToString(self, wantHeaders ? 1 : 0, out var result,
      out var status);
    return status.Unwrap(result);
  }

  private void ReleaseUnmanagedResources() {
    var temp = _self.Reset();
    if (temp.IsNull) {
      return;
    }
    deephaven_dhclient_utility_TableMaker_dtor(temp);
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
