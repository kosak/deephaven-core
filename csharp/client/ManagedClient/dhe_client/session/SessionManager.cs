using System.Text.Json;
using Deephaven.DheClient.Auth;
using Deephaven.DheClient.Controller;
using Deephaven.ManagedClient;
using Google.Protobuf;

namespace Deephaven.DheClient.Session;

public class SessionManager : IDisposable {
  private const string DefaultOverrideAuthority = "authserver";

  private class SessionInfo {
    public required string[] auth_host { get; set; }
    public required UInt16 auth_port { get; set; }
    public required string controller_host { get; set; }
    public required UInt16 controller_port { get; set; }
    public string? truststore_url { get; set; }
    public bool override_authorities { get; set; }
    public string? auth_authority { get; set; }
    public string? controller_authority { get; set; }
  }

  public static SessionManager FromUrl(string descriptiveName, string url, bool validateCertificate = true) {
    var json = GetUrl(url, validateCertificate);
    return FromJson(descriptiveName, json);
  }

  public static SessionManager FromJson(string descriptiveName, string json) {
    try {
      var info = JsonSerializer.Deserialize<SessionInfo>(json);
      if (info == null) {
        // Can this happen?
        throw new Exception("Deserialize returned null");
      }

      string? rootCerts = null;
      if (info.truststore_url != null) {
        // TODO(kosak): true, false, or pass through some parameter?
        rootCerts = GetUrl(info.truststore_url, false);
      }

      string? authAuthority = null;
      string? controllerAuthority = null;
      if (info.override_authorities) {
        authAuthority = info.auth_authority ?? DefaultOverrideAuthority;
        controllerAuthority = info.controller_authority ?? DefaultOverrideAuthority;
      }

      return Create(
        descriptiveName,
        info.auth_host[0], info.auth_port,
        authAuthority,
        info.controller_host, info.controller_port,
        controllerAuthority,
        rootCerts);
    } catch (Exception e) {
      throw new Exception($"Error processing JSON document: {e}");
    }
  }

  public static SessionManager Create(
    string descriptiveName,
    string authHost, UInt16 authPort, string authAuthority,
    string controllerHost, UInt16 controllerPort, string controllerAuthority,
    string rootCerts) {
    var (authTarget, authOptions) = SetupClientOptions(authHost, authPort,
      authAuthority, rootCerts);
    var authClient = AuthClient.Connect(descriptiveName, authTarget, authOptions);
    var (controllerTarget, controllerOptions) = SetupClientOptions(
      controllerHost, controllerPort,
      controllerAuthority, rootCerts);
    var controllerClient = ControllerClient.Connect(descriptiveName,
      controllerTarget, controllerOptions);
    return new SessionManager(descriptiveName, authClient, controllerClient,
      authAuthority, controllerAuthority, rootCerts);
  }

  private static (string, ClientOptions) SetupClientOptions(string host, UInt16 port,
    string? overrideAuthority, string rootCerts) {
    var target = $"{host}:{port}";
    var clientOptions = new ClientOptions {
      UseTls = true,
      TlsRootCerts = rootCerts
    };
    if (overrideAuthority != null) {
      // clientOptions.AddStringOption(GRPC_SSL_TARGET_NAME_OVERRIDE_ARG, override_authority);
    }

    return (target, clientOptions);
  }

  private readonly string _logId;
  private readonly AuthClient _authClient;
  private readonly ControllerClient _controllerClient;
  private readonly string _authAuthority;
  private readonly string _controllerAuthority;
  private readonly string _rootCerts;

  private SessionManager(string logId, AuthClient authClient, ControllerClient controllerClient,
    string authAuthority, string controllerAuthority, string rootCerts) {
    _logId = logId;
    _authClient = authClient;
    _controllerClient = controllerClient;
    _authAuthority = authAuthority;
    _controllerAuthority = controllerAuthority;
    _rootCerts = rootCerts;
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public bool PasswordAuthentication(string user, string password, string operateAs) {
    var authResult = _authClient.PasswordAuthentication(user, password, operateAs);
    var authOk = true;
    MegaCookie666.cookie = authResult.Cookie;
    return authOk && AuthenticateToController();
  }

  public DndClient ConnectToPqByName(string pqName, bool removeOnClose) {
    throw new NotImplementedException();
  }

  private static string GetUrl(string url, bool validateCertificate) {
    var handler = new HttpClientHandler();
    if (!validateCertificate) {
      handler.ClientCertificateOptions = ClientCertificateOption.Manual;
      handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    }
    using var hc = new HttpClient(handler);
    var result = hc.GetStringAsync(url).Result;
    return result;
  }

  private bool AuthenticateToController() {
    var authToken = _authClient.CreateToken(ControllerClient.ControllerServiceName);
    return _controllerClient.Authenticate(authToken);
  }

  public void SuperPain666() {
    _controllerClient.Superpain666();
  }
}

public static class MegaCookie666 {
  public static ByteString cookie;
}