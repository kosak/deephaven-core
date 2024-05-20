using System;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DeephavenClient;

public class DurationSpecifier {
  private readonly object _duration;

  public DurationSpecifier(Int64 nanos) => _duration = nanos;
  public DurationSpecifier(string duration) => _duration = duration;

  public static implicit operator DurationSpecifier(Int64 nanos) => new (nanos);
  public static implicit operator DurationSpecifier(string duration) => new (duration);

  internal InternalDurationSpecifier Materialize() => new (_duration);
}

internal class InternalDurationSpecifier : IDisposable {
  internal NativePtr<NativeDurationSpecifier> Self;

  public InternalDurationSpecifier(object duration) {
    NativePtr<NativeDurationSpecifier> result;
    ErrorStatus status;
    if (duration is Int64 nanos) {
      NativeDurationSpecifier.deephaven_client_utility_DurationSpecifier_ctor_nanos(nanos,
        out result, out status);
    } else if (duration is string dur) {
      NativeDurationSpecifier.deephaven_client_utility_DurationSpecifier_ctor_duration(dur,
        out result, out status);
    } else {
      throw new ArgumentException($"Unexpected type {duration.GetType().Name} for duration");
    }
    status.OkOrThrow();
    Self = result;
  }

  ~InternalDurationSpecifier() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  private void ReleaseUnmanagedResources() {
    if (!NativePtrUtil.TryRelease(ref Self, out var old)) {
      return;
    }
    NativeDurationSpecifier.deephaven_client_utility_DurationSpecifier_dtor(old);
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
