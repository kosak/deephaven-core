using System.Text.Json;
using Deephaven.ManagedClient;

namespace DeephavenEnterprise.DndClient;

static class ZamboniUtility {
  public static string GetUrl(string url) {
    var handler = new HttpClientHandler();
    // if (!corePlus.ValidateCertificate) {
    //   handler.ClientCertificateOptions = ClientCertificateOption.Manual;
    //   handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    // }
    var hc = new HttpClient(handler);
    var result = hc.GetStringAsync(url).Result;
    return result;
  }
}

public struct SessionInfo {
  public required string[] auth_host { get; set; }
  public required Int16 auth_port { get; set; }
  public required string controller_host { get; set; }
  public required Int16 controller_port { get; set; }
  public required bool? override_authorities { get; set; }
  public string? auth_authority { get; set; }
  public string? controller_authority { get; set; }
  public string? truststore_url { get; set; }
}

public class SessionManager {
  private const string DefaultOverrideAuthority = "authserver";

  public static SessionManager FromJson(string descriptiveName, string jsonStr) {
    var si = JsonSerializer.Deserialize<SessionInfo>(jsonStr);
    var authAuthority = "";
    var controllerAuthority = "";
    if (si.override_authorities.HasValue && si.override_authorities.Value) {
      authAuthority = si.auth_authority ?? DefaultOverrideAuthority;
      controllerAuthority = si.controller_authority ?? DefaultOverrideAuthority;
    }

    var rootCerts = si.truststore_url != null ? ZamboniUtility.GetUrl(si.truststore_url) : "";

    return Create(descriptiveName, si.auth_host[0], si.auth_port, authAuthority,
      si.controller_host, si.controller_port, controllerAuthority,
      rootCerts);
  }



  public static SessionManager Create(string descriptiveName,
    string authHost, Int16 authPort, string authAuthority,
    string controllerHost, Int16 controllerPort, string controllerAuthority,
    string rootCerts) {
    var (authTarget, authOptions) = SetupClientOptions(authHost, authPort, authAuthority, rootCerts);
    var authClient = AuthClient.Connect(descriptiveName, authTarget, authOptions);
    var (controllerTarget, controllerOptions) = SetupClientOptions(controllerHost,
      controllerPort, controllerAuthority, rootCerts);
    var controllerClient = ControllerClient.Connect(descriptiveName, controllerTarget,
      controllerOptions);
    return new SessionManager(descriptiveName, authClient, controllerClient,
      authAuthority, controllerAuthority, rootCerts);
  }

  private readonly string _descriptiveName;
  private readonly AuthClient _authClient;
  private readonly ControllerClient _controllerClient;
  private readonly string _authAuthority;
  private readonly string _controllerAuthority;
  private readonly string _rootCerts;

  private SessionManager(string descriptiveName, AuthClient authClient,
    ControllerClient controllerClient, string authAuthority, string controllerAuthority,
    string rootCerts) {
    _descriptiveName = descriptiveName;
    _authClient = authClient;
    _controllerClient = controllerClient;
    _authAuthority = authAuthority;
    _controllerAuthority = controllerAuthority;
    _rootCerts = rootCerts;
  }

  private static (string, ClientOptions) SetupClientOptions(string host, Int16 port, string overrideAuthority,
    string rootCerts) {
    var target = $"{host}:{port}";
    var clientOptions = new ClientOptions();
    clientOptions.SetUseTls(true);
    clientOptions.SetTlsRootCerts(rootCerts);
    if (!overrideAuthority.IsEmpty()) {
      // uh oh
      // client_options.AddStringOption(
      //   GRPC_SSL_TARGET_NAME_OVERRIDE_ARG, override_authority);
    }

    return (target, clientOptions);
  }

  public bool PasswordAuthentication(string user, string password, string operateAs) {
    return _authClient.PasswordAuthentication(user, password, operateAs);
  }

}

//   std::string auth_authority;
//   std::string controller_authority;
//   if (json.contains(kJsonOverrideAuthority)) {
//     if (json.find(kJsonOverrideAuthority)->get<bool>()) {
//       if (json.contains(kJsonAuthAuthority)) {
//         auth_authority = json.find(kJsonAuthAuthority)->get < std::string> ();
//       } else {
//         auth_authority = kDefaultOverrideAuthority;
//       }
//       if (json.contains(kJsonControllerAuthority)) {
//         controller_authority = json.find(kJsonControllerAuthority)->get < std::string> ();
//       } else {
//         controller_authority = kDefaultOverrideAuthority;
//       }
//     }
//   }
//   return Create(
//       descriptive_name,
//       auth_host, auth_port, auth_authority,
//       controller_host, controller_port, controller_authority,
//       root_certs);
// } catch (json::exception &e) {
//   throw std::runtime_error(DEEPHAVEN_LOCATION_STR(
//       std::string("Error processing JSON document:") + e.what()));
// }
//
//
//    }
