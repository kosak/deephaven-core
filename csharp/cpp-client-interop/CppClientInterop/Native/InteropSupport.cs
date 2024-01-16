using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.CppClientInterop.Native;

[StructLayout(LayoutKind.Sequential)]
public struct NativePtr<T> {
  public IntPtr ptr;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct NativeError {
  public string error;

  public T Unwrap<T>(T item) {
    if (error == null) {
      return item;
    }
    throw new Exception(error);
  }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct ErrorStatus {
  public string error;

  public void OkOrThrow() {
    if (error != null) {
      throw new Exception(error);
    }
  }

  public T Unwrap<T>(T item) {
    OkOrThrow();
    return item;
  }
}

[StructLayout(LayoutKind.Sequential)]
public struct ResultOrError<T> {
  public NativePtr<T> result;
  public NativePtr<WrappedException> error;

  public NativePtr<T> Unwrap() {
    if (error.ptr == IntPtr.Zero) {
      // Result is typically not null but ocassionally (as when returning Void results, it is zero)
      return result;
    }

    var what = WrappedException.deephaven_client_WrappedException_What(error);
    WrappedException.deephaven_client_WrappedException_dtor(error);
    throw new Exception(what);
  }
}

[StructLayout(LayoutKind.Sequential)]
public struct ResultOrError2<T> {
  public NativePtr<WrappedException> error;
  public T result;

  public T Unwrap() {
    if (error.ptr == IntPtr.Zero) {
      return result;
    }

    var what = WrappedException.deephaven_client_WrappedException_What(error);
    WrappedException.deephaven_client_WrappedException_dtor(error);
    throw new Exception(what);
  }
}
