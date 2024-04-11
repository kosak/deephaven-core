using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deephaven.CppClientInterop.Native;

namespace Deephaven.CppClientInterop;

public class SubscriptionHandle : IDisposable {
  private NativePtr<Native.SubscriptionHandle> nativeSubscriptionHandle;
  private readonly Object keepAlive;

  internal SubscriptionHandle(NativePtr<Native.SubscriptionHandle> nativeSubscriptionHandle, Object keepAlive) {
    this.nativeSubscriptionHandle = nativeSubscriptionHandle;
    this.keepAlive = keepAlive;
  }

  internal NativePtr<Native.SubscriptionHandle> ReleaaseSubscriptionHandle() {
    if (nativeSubscriptionHandle.ptr == IntPtr.Zero) {
      throw new Exception("Subscription handle already released");
    }
    var result = nativeSubscriptionHandle;
    nativeSubscriptionHandle.ptr = IntPtr.Zero;
    return result;
  }
}

public class TickingUpdate : IDisposable {
  private NativePtr<Native.TickingUpdate> nativeTickingUpdate;

  internal TickingUpdate(NativePtr<Native.TickingUpdate> nativeTickingUpdate) =>
    this.nativeTickingUpdate = nativeTickingUpdate;

  public ClientTable Current { get;  }
  // public ClientTable BeforeRemoves { get;  }
  // public RowSequence RemovedRows { get;  }
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
