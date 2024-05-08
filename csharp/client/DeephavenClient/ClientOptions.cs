using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient;

public class ClientOptions : IDisposable {
  internal NativePtr<NativeClientOptions> Self;

  public ClientOptions() {
    NativeClientOptions.deephaven_client_ClientOptions_ctor(out var result, out var status);
    status.OkOrThrow();
    Self = result;
  }

  ~ClientOptions() {
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
    NativeClientOptions.deephaven_client_ClientOptions_dtor(temp);
  }

  ClientOptions SetDefaultAuthentication() {
    NativeClientOptions.deephaven_client_ClientOptions_SetDefaultAuthentication(Self,
      out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions SetBasicAuthentication(string username, string password) {
    NativeClientOptions.deephaven_client_ClientOptions_SetBasicAuthentication(Self,
      username, password, out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions SetCustomAuthentication(string authenticationKey, string authenticationValue) {
    NativeClientOptions.deephaven_client_ClientOptions_SetCustomAuthentication(Self,
      authenticationKey, authenticationValue, out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions SetSessionType(string sessionType) {
    NativeClientOptions.deephaven_client_ClientOptions_SetSessionType(Self, sessionType,
      out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions SetUseTls(bool useTls) {
    NativeClientOptions.deephaven_client_ClientOptions_SetUseTls(Self, useTls,
      out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions SetTlsRootCerts(string tlsRootCerts) {
    NativeClientOptions.deephaven_client_ClientOptions_SetTlsRootCerts(Self, tlsRootCerts,
      out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions SetClientCertChain(string clientCertChain) {
    NativeClientOptions.deephaven_client_ClientOptions_SetClientCertChain(Self, clientCertChain,
      out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions SetClientPrivateKey(string clientPrivateKey) {
    NativeClientOptions.deephaven_client_ClientOptions_SetClientPrivateKey(Self, clientPrivateKey,
      out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions AddIntOption(string opt, Int32 val) {
    NativeClientOptions.deephaven_client_ClientOptions_AddIntOption(Self, opt, val,
      out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions AddStringOption(string opt, string val) {
    NativeClientOptions.deephaven_client_ClientOptions_AddStringOption(Self, opt, val,
      out var status);
    status.OkOrThrow();
    return this;
  }

  ClientOptions AddExtraHeader(string headerName, string headerValue) {
    NativeClientOptions.deephaven_client_ClientOptions_AddExtraHeader(Self, headerName, headerValue,
      out var status);
    status.OkOrThrow();
    return this;
  }
}


internal class NativeClientOptions {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_ctor(
    out NativePtr<NativeClientOptions> result, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_dtor(NativePtr<NativeClientOptions> self);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_SetDefaultAuthentication(NativePtr<NativeClientOptions> self,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_SetBasicAuthentication(NativePtr<NativeClientOptions> self,
    string username, string password, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_SetCustomAuthentication(NativePtr<NativeClientOptions> self,
    string authentication_key, string authentication_value, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_SetSessionType(NativePtr<NativeClientOptions> self,
    string session_type, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_SetUseTls(NativePtr<NativeClientOptions> self,
    bool use_tls, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_SetTlsRootCerts(NativePtr<NativeClientOptions> self,
    string tls_root_certs, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_SetClientCertChain(NativePtr<NativeClientOptions> self,
    string client_cert_chain, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_SetClientPrivateKey(NativePtr<NativeClientOptions> self,
    string client_private_key, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_AddIntOption(NativePtr<NativeClientOptions> self,
    string opt, Int32 val, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_AddStringOption(NativePtr<NativeClientOptions> self,
    string opt, string val, out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_ClientOptions_AddExtraHeader(NativePtr<NativeClientOptions> self,
    string header_name, string header_value, out ErrorStatus status);
}
