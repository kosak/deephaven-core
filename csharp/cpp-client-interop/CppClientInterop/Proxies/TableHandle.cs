using Deephaven.CppClientInterop.Native;
using System;
namespace Deephaven.CppClientInterop;

public sealed class TableHandle : IDisposable {
  internal NativePtr<Native.TableHandle> self;

  internal TableHandle(NativePtr<Native.TableHandle> self) {
    this.self = self;
  }

  ~TableHandle() {
    Dispose();
  }

  public void Dispose() {
    if (self.ptr == IntPtr.Zero) {
      return;
    }
    Native.TableHandle.deephaven_client_TableHandle_dtor(self);
    self.ptr = IntPtr.Zero;
    GC.SuppressFinalize(this);
  }

  public TableHandle Update(params string[] columnSpecs) {
    Native.TableHandle.deephaven_client_TableHandle_Update(self, columnSpecs, columnSpecs.Length, out var roe);
    var res = roe.Unwrap();
    return new TableHandle(res);
  }

  public void BindToVariable(string variable) {
    Native.TableHandle.deephaven_client_TableHandle_BindToVariable(self, variable, out var roe);
    _ = roe.Unwrap();
  }

  private class TickingWrapper {
    private readonly ITickingCallback _callback;

    public TickingWrapper(ITickingCallback callback) => this._callback = callback;

    public void NativeOnUpdate(NativePtr<Native.TickingUpdate> nativeTickingUpdate) {
      var tickingUpdate = new TickingUpdate(nativeTickingUpdate);
      _callback.OnTick(tickingUpdate);
    }
  }

  public SubscriptionHandle Subscribe(ITickingCallback callback) {
    var zm = new TickingWrapper(callback);
    Native.TableHandle.deephaven_client_TableHandle_Subscribe(self, zm.NativeOnUpdate, callback.OnFailure,
      out var nativeSusbcriptionHandle, out var status);
    status.OkOrThrow();
    return new SubscriptionHandle(nativeSusbcriptionHandle, zm);
  }

  public void Unsubscribe(SubscriptionHandle handle) {
    Native.TableHandle.deephaven_client_TableHandle_Unsubscribe(self, handle.NativeSubscriptionHandle, out var status);
    status.OkOrFail();
  }

  public ArrowTable ToArrowTable() {
    Native.TableHandle.deephaven_client_TableHandle_ToArrowTable(self, out var arrowTable, out var numColumns,
      out var numRows, out var status);
    status.OkOrThrow();
    return new ArrowTable(arrowTable, numColumns, numRows);
  }

  public ClientTable ToClientTable() {
    Native.TableHandle.deephaven_client_TableHandle_ToClientTable(self, out var arrowTable, out var numColumns,
      out var numRows, out var status);
    status.OkOrThrow();
    return new ClientTable(arrowTable, numColumns, numRows);
  }

  public string ToString(bool wantHeaders) {
    Native.TableHandle.deephaven_client_TableHandle_ToString(self, wantHeaders ? 1 : 0, out var result,
      out var status);
    return status.Unwrap(result);
  }
}
