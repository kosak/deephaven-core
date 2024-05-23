using System;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DeephavenClient.UpdateBy;

public enum MathContext : Int32 {
  Unlimited, Decimal32, Decimal64, Decimal128
};

public enum BadDataBehavior : Int32 {
  Reset, Skip, Throw, Poison
};

public enum DeltaControl : Int32 {
  NullDominates, ValueDominates, ZeroDominates
};

public readonly struct OperationControl {
  public readonly BadDataBehavior OnNull;
  public readonly BadDataBehavior OnNaN;
  public readonly MathContext BigValueContext;

  public OperationControl(BadDataBehavior onNull = BadDataBehavior.Skip,
    BadDataBehavior onNaN = BadDataBehavior.Skip,
    MathContext bigValueContext = MathContext.Decimal128) {
    OnNull = onNull;
    OnNaN = onNaN;
    BigValueContext = bigValueContext;
  }
}

public sealed class UpdateByOperation {
  private delegate void NativeInvoker(out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);

  private readonly NativeInvoker _invoker;

  private UpdateByOperation(NativeInvoker invoker) => _invoker = invoker;

  internal InternalUpdateByOperation MakeInternal() {
    _invoker(out var result, out var status);
    status.OkOrThrow();
    return new InternalUpdateByOperation(result);
  }


  public static UpdateByOperation CumSum(string[] cols) =>
    new UpdateByOperation(cols, NativeUpdateByOperation.deephaven_client_update_by_cumSum);
  public static UpdateByOperation CumProd(string[] cols) =>
    new UpdateByOperation(cols, NativeUpdateByOperation.deephaven_client_update_by_cumProd);
  public static UpdateByOperation CumMin(string[] cols) =>
    new UpdateByOperation(cols, NativeUpdateByOperation.deephaven_client_update_by_cumMin);
  public static UpdateByOperation CumMax(string[] cols) =>
    new UpdateByOperation(cols, NativeUpdateByOperation.deephaven_client_update_by_cumMax);
  public static UpdateByOperation ForwardFill(string[] cols) =>
    new UpdateByOperation(cols, NativeUpdateByOperation.deephaven_client_update_by_forwardFill);

  public static UpdateByOperation Delta(string[] cols, DeltaControl deltaControl = DeltaControl.NullDominates) =>
    new UpdateByOperation((out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status) =>
      NativeUpdateByOperation.deephaven_client_update_by_delta(cols, cols.Length, deltaControl, out result, out status));

  public static UpdateByOperation EmaTick(double decayTicks, string[] cols, OperationControl? opControl) {
    opControl ??= new OperationControl();
    return new UpdateByOperation((out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status) =>
      NativeUpdateByOperation.deephaven_client_update_by_emaTick(decayTicks, cols, cols.Length, ref opControl, out result,
        out status));
  }

  public static UpdateByOperation EmaTime(string timestampCol, DurationSpecifier decayTime, string[] cols, OperationControl? opControl) {
    opControl ??= new OperationControl();
    return new UpdateByOperation((out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status) =>
      NativeUpdateByOperation.deephaven_client_update_by_emaTime(decayTicks, cols, cols.Length, ref opControl, out result,
        out status));
  }

  public static UpdateByOperation EmsTick(double decayTicks, string[] cols, OperationControl? opControl) {
    opControl ??= new OperationControl();
    return new UpdateByOperation((out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status) =>
      NativeUpdateByOperation.deephaven_client_update_by_emsTick(decayTicks, cols, cols.Length, ref opControl, out result,
        out status));
  }

  public static UpdateByOperation EmsTime(string timestampCol, DurationSpecifier decayTime, string[] cols, OperationControl? opControl) {
    opControl ??= new OperationControl();
    return new UpdateByOperation((out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status) =>
      NativeUpdateByOperation.deephaven_client_update_by_emsTime(decayTicks, cols, cols.Length, ref opControl, out result,
        out status));
  }

  public static UpdateByOperation EmminTick(double decayTicks, string[] cols, OperationControl? opControl) {
    opControl ??= new OperationControl();
    return new UpdateByOperation((out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status) =>
      NativeUpdateByOperation.deephaven_client_update_by_emminTick(decayTicks, cols, cols.Length, ref opControl, out result,
        out status));
  }

  public static UpdateByOperation EmminTime(string timestampCol, DurationSpecifier decayTime, string[] cols, OperationControl? opControl) {
    opControl ??= new OperationControl();
    return new UpdateByOperation((out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status) =>
      NativeUpdateByOperation.deephaven_client_update_by_emminTime(decayTicks, cols, cols.Length, ref opControl, out result,
        out status));
  }


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
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_update_by_cumProd(string[] cols, Int32 numCols,
    out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_update_by_cumMin(string[] cols, Int32 numCols,
    out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_update_by_cumMax(string[] cols, Int32 numCols,
    out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_update_by_forwardFill(string[] cols, Int32 numCols,
    out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_client_update_by_delta(string[] cols, Int32 numCols,
    DeltaControl deltaControl, out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
}
