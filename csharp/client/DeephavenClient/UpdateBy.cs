using System;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DeephavenClient;

public sealed class UpdateByOperation {
  private delegate void NativeInvoker(string[] cols, Int32 numCols,
    out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);

  private readonly string[] _cols;
  private readonly NativeInvoker _invoker;

  private UpdateByOperation(string[] cols, NativeInvoker invoker) {
    _cols = cols;
    _invoker = invoker;
  }

  internal InternalUpdateByOperation MakeInternal() {
    _invoker(_cols, _cols.Length, out var result, out var status);
    status.OkOrThrow();
    return new InternalUpdateByOperation(result);
  }

  public static UpdateByOperation CumSum(string[] cols) =>
    new UpdateByOperation(cols, NativeUpdateByOperation.deephaven_client_update_by_cumSum);
}

internal class InternalUpdateByOperation : IDisposable {
  internal NativePtr<NativeUpdateByOperation> Self;

  internal InternalUpdateByOperation(NativePtr<NativeUpdateByOperation> self) => Self = self;

  ~InternalUpdateByOperation() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  private void ReleaseUnmanagedResources() {
    if (!Self.TryRelease(out var old)) {
      return;
    }
    NativeUpdateByOperation.deephaven_client_UpdateByOperation_dtor(old);
  }
}

internal partial class NativeUpdateByOperation {
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_UpdateByOperation_dtor(NativePtr<NativeUpdateByOperation> self);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_update_by_cumSum(string[] cols, Int32 numCols,
    out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
}
