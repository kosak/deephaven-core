using System;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DeephavenClient;

public class DurationSpecifier {
  internal NativePtr<NativeDurationSpecifier> Self;

  public DurationSpecifier(Int64 nanos) {
    NativeDurationSpecifier.deephaven_client_utility_DurationSpecifier_ctor_nanos(nanos,
      out var result, out var status);
    status.OkOrThrow();
    Self = result;
  }

  public DurationSpecifier(string duration) {
    NativeDurationSpecifier.deephaven_client_utility_DurationSpecifier_ctor_duration(duration,
      out var result, out var status);
    status.OkOrThrow();
    Self = result;
  }

  ~DurationSpecifier() {
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
    NativeDurationSpecifier.deephaven_client_utility_DurationSpecifier_dtor(temp);
  }
}

internal class NativeDurationSpecifier {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_utility_DurationSpecifier_ctor_nanos(Int64 nanos,
    out NativePtr<NativeDurationSpecifier> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_utility_DurationSpecifier_ctor_duration(string duration,
    out NativePtr<NativeDurationSpecifier> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_utility_DurationSpecifier_dtor(NativePtr<NativeDurationSpecifier> self);
}
