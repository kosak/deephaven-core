using Deephaven.DeephavenClient;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Factories;

internal static class ConnectionFactory {
  public static Client ConnectToCore(CoreCredentials credentials) {
    var options = new ClientOptions();
    options.SetSessionType(credentials.SessionTypeIsPython ? "python" : "groovy");
    var client = Client.Connect(credentials.ConnectionString, options);
    return client;
  }

  public static SessionManager ConnectToCorePlus(CorePlusCredentials credentials, WorkerThread workerThread) {
    var handler = new HttpClientHandler();
    if (!credentials.ValidateCertificate) {
      handler.ClientCertificateOptions = ClientCertificateOption.Manual;
      handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    }

    var hc = new HttpClient(handler);
    var json = hc.GetStringAsync(credentials.JsonUrl).Result;
    var session = SessionManager.FromJson("Deephaven Excel", json);
    if (!session.PasswordAuthentication(credentials.User, credentials.Password, credentials.OperateAs)) {
      session.Dispose();
      throw new Exception("Authentication failed");
    }

    return session;
  }
}
