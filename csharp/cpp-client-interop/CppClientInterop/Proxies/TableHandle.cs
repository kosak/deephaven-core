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

  public string ToString(bool wantHeaders) {
    Native.TableHandle.deephaven_client_TableHandle_ToString(self, wantHeaders ? 1 : 0, out var result,
      out var status);
    return status.Unwrap(result);
  }

  public ArrowTable ToArrowTable() {
    Native.TableHandle.deephaven_client_TableHandle_ToArrowTable(self, out var arrowTable, out var numColumns,
      out var numRows, out var status);
    status.OkOrThrow();
    return new ArrowTable(arrowTable, numColumns, numRows);
  }
}
