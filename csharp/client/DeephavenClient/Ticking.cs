using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient;

public class SubscriptionHandle : IDisposable {
  internal NativePtr<NativeSubscriptionHandle> Self;

  internal SubscriptionHandle(NativePtr<NativeSubscriptionHandle> self) {
    Self = self;
  }

  ~SubscriptionHandle() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  private void ReleaseUnmanagedResources() {
    var temp = Self.Release();
    if (temp.IsNull) {
      return;
    }
    NativeSubscriptionHandle.deephaven_client_SubscriptionHandle_dtor(temp);
  }
}

public class TickingUpdate : IDisposable {
  internal NativePtr<NativeTickingUpdate> Self;

  internal TickingUpdate(NativePtr<NativeTickingUpdate> self) => this.Self = self;

  public ClientTable Current {
    get {
      NativeTickingUpdate.deephaven_client_TickingUpdate_Current(Self,
        out var ct, out var status);
      status.OkOrThrow();
      return new ClientTable(ct);
    }
  }
  // public ClientTable BeforeRemoves { get;  }
  // public RowSequence RemovedRows { get;  }

  ~TickingUpdate() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  public void ReleaseUnmanagedResources() {
    var temp = Self.Release();
    if (temp.IsNull) {
      return;
    }
    NativeTickingUpdate.deephaven_client_TickingUpdate_dtor(temp);
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
