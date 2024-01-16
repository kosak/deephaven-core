using Deephaven.CppClientInterop.Native;

namespace Deephaven.CppClientInterop;

public class DurationSpecifier {
  internal NativePtr<Native.DurationSpecifier> self;

  public DurationSpecifier(Int64 nanos) {
    Native.DurationSpecifier.deephaven_client_DurationSpecifier_ctor_nanos(nanos, out var roe);
    self = roe.Unwrap();
  }

  public DurationSpecifier(string duration) {
    Native.DurationSpecifier.deephaven_client_DurationSpecifier_ctor_duration(duration, out var roe);
    self = roe.Unwrap();
  }

  ~DurationSpecifier() {
    Dispose();
  }

  public void Dispose() {
    if (self.ptr == IntPtr.Zero) {
      return;
    }
    Native.DurationSpecifier.deephaven_client_DurationSpecifier_dtor(self);
    self.ptr = IntPtr.Zero;
    GC.SuppressFinalize(this);
  }

}

public class TimePointSpecifier {
  internal NativePtr<Native.TimePointSpecifier> self;

  public TimePointSpecifier(Int64 nanos) {
    Native.TimePointSpecifier.deephaven_client_TimePointSpecifier_ctor_nanos(nanos, out var roe);
    self = roe.Unwrap();
  }

  public TimePointSpecifier(string duration) {
    Native.TimePointSpecifier.deephaven_client_TimePointSpecifier_ctor_duration(duration, out var roe);
    self = roe.Unwrap();
  }

  ~TimePointSpecifier() {
    Dispose();
  }

  public void Dispose() {
    if (self.ptr == IntPtr.Zero) {
      return;
    }
    Native.TimePointSpecifier.deephaven_client_TimePointSpecifier_dtor(self);
    self.ptr = IntPtr.Zero;
    GC.SuppressFinalize(this);
  }
}
