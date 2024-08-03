using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DeephavenEnterpriseClient.session;

public class SessionManager : IDisposable {
  internal MyNativePtr2 Self;

  public static SessionManager FromUrl(string descriptiveName, string jsonUrl) {
    NativeSessionManager.deephaven_enterprise_session_SessionManager_FromJson(descriptiveName,
      jsonUrl, out var sessionResult, out var status);
    status.OkOrThrow();
    return null;

  }

  private SessionManager(MyNativePtr2 self) {
    Self = self;
  }

  ~SessionManager() {
    ReleaseUnmanagedResources();
  }

  public void Close() {
    Dispose();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  private void ReleaseUnmanagedResources() {
    if (!Self.TryRelease(out var old)) {
      return;
    }
  }
}


[StructLayout(LayoutKind.Sequential)]
public struct MyNativePtr<T> {
  public IntPtr ptr;

  public MyNativePtr(IntPtr ptr) => this.ptr = ptr;

  public bool TryRelease(out MyNativePtr<T> oldPtr) {
    oldPtr = new MyNativePtr<T>(ptr);
    if (IsNull) {
      return false;
    }

    ptr = IntPtr.Zero;
    return true;
  }

  public readonly bool IsNull => ptr == IntPtr.Zero;
}

struct MyNativePtr3 {
  public MyNativePtr2 hate;

}

[StructLayout(LayoutKind.Sequential)]
struct ErrorStatus3 {
  private ErrorStatus inner;

  public void OkOrThrow() {
  }
}


internal partial class NativeSessionManager {
  [LibraryImport(LibraryPaths.DhEnterprise, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_enterprise_session_SessionManager_FromJson(string descriptiveName,
    string json, out MyNativePtr3 result, out ErrorStatus3 status);
}
