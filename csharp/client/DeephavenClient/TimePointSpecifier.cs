using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.DeephavenClient;

public class TimePointSpecifier : IDisposable {
  internal NativePtr<NativeTimePointSpecifier> Self;

  public TimePointSpecifier(Int64 nanos) {
    NativeTimePointSpecifier.deephaven_client_utility_TimePointSpecifier_ctor_nanos(nanos,
      out var result, out var status);
    status.OkOrThrow();
    Self = result;
  }

  public TimePointSpecifier(string duration) {
    NativeTimePointSpecifier.deephaven_client_utility_TimePointSpecifier_ctor_duration(duration,
      out var result, out var status);
    status.OkOrThrow();
    Self = result;
  }

  ~TimePointSpecifier() {
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
    NativeTimePointSpecifier.deephaven_client_utility_TimePointSpecifier_dtor(temp);
  }
}

internal class NativeTimePointSpecifier {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_utility_TimePointSpecifier_ctor_nanos(Int64 nanos,
    out NativePtr<NativeTimePointSpecifier> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_utility_TimePointSpecifier_ctor_duration(string duration,
    out NativePtr<NativeTimePointSpecifier> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_utility_TimePointSpecifier_dtor(NativePtr<NativeTimePointSpecifier> self);
}
