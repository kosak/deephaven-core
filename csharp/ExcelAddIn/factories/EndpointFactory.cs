using Deephaven.Dh_NetClient;
using Deephaven.Dhe_NetClient;
using Deephaven.ExcelAddIn.Models;

namespace Deephaven.ExcelAddIn.Factories;

internal static class EndpointFactory {
  public static Client ConnectToCore(CoreEndpointConfig config) {
    var client = Client.Connect(config.ConnectionString);
    return client;
  }

  public static SessionManager ConnectToCorePlus(CorePlusEndpointConfig config) {
    var handler = new HttpClientHandler();
    if (!config.ValidateCertificate) {
      handler.ClientCertificateOptions = ClientCertificateOption.Manual;
      handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    }

    var credentials = Credentials.OfUsernamePassword(config.User, config.Password,
      config.OperateAs);
    var hc = new HttpClient(handler);
    var json = hc.GetStringAsync(config.JsonUrl).Result;
    var session = SessionManager.FromJson("Deephaven Excel", credentials, json);
    return session;
  }
}
