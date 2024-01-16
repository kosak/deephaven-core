using Deephaven.CppClientInterop.Native;

namespace Deephaven.CppClientInterop;

public class ClientOptions : IDisposable {
  internal NativePtr<Native.ClientOptions> self;

  public ClientOptions() {
    Native.ClientOptions.deephaven_client_ClientOptions_ctor(out var roe);
    self = roe.Unwrap();
  }

  ~ClientOptions() {
    Dispose();
  }

  public void Dispose() {
    if (self.ptr == IntPtr.Zero) {
      return;
    }
    Native.ClientOptions.deephaven_client_ClientOptions_dtor(self);
    self.ptr = IntPtr.Zero;
    GC.SuppressFinalize(this);
  }

  ClientOptions SetDefaultAuthentication() {
    Native.ClientOptions.deephaven_client_ClientOptions_SetDefaultAuthentication(self, out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions SetBasicAuthentication(string username, string password) {
    Native.ClientOptions.deephaven_client_ClientOptions_SetBasicAuthentication(self, username, password,
      out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions SetCustomAuthentication(string authenticationKey, string authenticationValue) {
    Native.ClientOptions.deephaven_client_ClientOptions_SetCustomAuthentication(self, authenticationKey, authenticationValue,
      out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions SetSessionType(string sessionType) {
    Native.ClientOptions.deephaven_client_ClientOptions_SetSessionType(self, sessionType, out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions SetUseTls(bool useTls) {
    Native.ClientOptions.deephaven_client_ClientOptions_SetUseTls(self, useTls, out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions SetTlsRootCerts(string tlsRootCerts) {
    Native.ClientOptions.deephaven_client_ClientOptions_SetTlsRootCerts(self, tlsRootCerts, out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions SetClientCertChain(string clientCertChain) {
    Native.ClientOptions.deephaven_client_ClientOptions_SetClientCertChain(self, clientCertChain, out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions SetClientPrivateKey(string clientPrivateKey) {
    Native.ClientOptions.deephaven_client_ClientOptions_SetClientPrivateKey(self, clientPrivateKey, out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions AddIntOption(string opt, Int32 val) {
    Native.ClientOptions.deephaven_client_ClientOptions_AddIntOption(self, opt, val, out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions AddStringOption(string opt, string val) {
    Native.ClientOptions.deephaven_client_ClientOptions_AddStringOption(self, opt, val, out var roe);
    _ = roe.Unwrap();
    return this;
  }

  ClientOptions AddExtraHeader(string headerName, string headerValue) {
    Native.ClientOptions.deephaven_client_ClientOptions_AddExtraHeader(self, headerName, headerValue, out var roe);
    _ = roe.Unwrap();
    return this;
  }
}
