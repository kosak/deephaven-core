using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient;

public class SubscriptionHandle {
  internal NativePtr<NativeSubscriptionHandle> Self;

  internal SubscriptionHandle(NativePtr<NativeSubscriptionHandle> self) {
    Self = self;
  }
}

public class TickingUpdate : IDisposable {
  private NativePtr<NativeTickingUpdate> self;

  internal TickingUpdate(NativePtr<NativeTickingUpdate> self) => this.self = self;

  public ClientTable Current {
    get {
      NativeTickingUpdate.deephaven_client_TickingUpdate_Current(self,
        out var ct, out var status);
      status.OkOrThrow();
      return new ClientTable(ct);
    }
  }
  // public ClientTable BeforeRemoves { get;  }
  // public RowSequence RemovedRows { get;  }

  ~TickingUpdate() {
    Dispose();
  }

  public void Dispose() {
    if (self.ptr == IntPtr.Zero) {
      return;
    }
    NativeTickingUpdate.deephaven_client_TickingUpdate_dtor(self);
    self.ptr = IntPtr.Zero;
    GC.SuppressFinalize(this);
  }
}

public interface ITickingCallback {
  /**
   * Invoked on each update to the subscription.
   */
  void OnTick(TickingUpdate update);

  /**
   * Invoked if there is an error involving the subscription.
   */
  void OnFailure(string errorMessage);
}

internal class NativeSubscriptionHandle {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_SubscriptionHandle_dtor(NativePtr<NativeSubscriptionHandle> self);
}

internal class NativeTickingUpdate {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_TickingUpdate_dtor(NativePtr<NativeTickingUpdate> self);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_TickingUpdate_Current(NativePtr<NativeTickingUpdate> self,
    out NativePtr<NativeClientTable> result, out ErrorStatus status);
}
