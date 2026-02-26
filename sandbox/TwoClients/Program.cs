using Deephaven.Dhe_NetClient;
using Deephaven.OpenAPI.Client;
using Deephaven.OpenAPI.Shared.Data;

namespace Deephaven.TwoClients;


internal class Program {
  private const string Uri = "https://kosak-grizzly-550.int.illumon.com:8123/iris/connection.json";
  private const string Host = "kosak-grizzly-550.int.illumon.com";
  private const int Port = 8123;

  static void Main(string[] args) {
    try {
      TestLegacy();
      TestModern();
    } catch (Exception e) {
      Console.Error.WriteLine(e);
    }
  }

  private static void TestLegacy() {
    var listener = new Listener();
    using var client = OpenApi.Connect(Host, openApiListener: listener, connectionTimeoutMillis: 10000, port: Port);
    client.Login("iris", "iris", "iris");
    using var scope = client.AttachWorkerByName("pq2legacy", listener); 
    using var t1 = scope.QueryScope.BoundTable("t1");
    PrintUtils.PrintTableData(t1);
  }

  private static void TestModern() {
    var creds = Credentials.OfUsernamePassword("iris", "iris", "iris");
    using var cModern = SessionManager.FromUri("hello", creds,
      "https://kosak-grizzly-550.int.illumon.com:8123/iris/connection.json");
    using var dndClient = cModern.ConnectToPqByName("pq1", false, false);
    using var t1 = dndClient.Manager.FetchTable("t1");
    Console.WriteLine(t1.ToString());
  }
}

public class Listener : IOpenApiListener, IWorkerListener {
  public void OnConnectionClosed() {
    Console.WriteLine("Connection closed");
  }
  public void OnConnectionFailed(Exception e) {
    Console.WriteLine("Connection failed: " + e);
  }
  public void OnConnectionOpened() {
    Console.WriteLine("Connection opened");
  }

  public void OnPersistentQueryAdded(IOpenApiClient openApiClient, IPersistentQueryConfig persistentQueryConfig) {
    Console.WriteLine("pq added");
  }

  public void OnPersistentQueryModified(IOpenApiClient openApiClient, IPersistentQueryConfig persistentQueryConfig) {
    Console.WriteLine("pq modified");
  }

  public void OnPersistentQueryRemoved(IOpenApiClient openApiClient, IPersistentQueryConfig persistentQueryConfig) {
    Console.WriteLine("pq removed");
  }

  public void OnClosed(IOpenApiClient openApiClient, ushort code, string reason) {
    Console.WriteLine("closed");
  }

  public void OnError(IOpenApiClient openApiClient, Exception exception) {
    Console.WriteLine("error");
  }

  public void OnOpen(IOpenApiClient openApiClient) {
    Console.WriteLine("open");
  }

  public void OnAuthTokenRefresh(IOpenApiClient openApiClient, RefreshToken authToken) {
    Console.WriteLine("token refresh");
  }

  public void OnAuthTokenError(IOpenApiClient openApiClient, string error) {
    Console.WriteLine("auth token error");
  }

  public void OnOpen(IWorkerSession workerSession) {
    Console.WriteLine("worker open");
  }

  public void OnClosed(IWorkerSession workerSession, ushort code, string err) {
    Console.WriteLine("worker closed");
  }

  public void OnError(IWorkerSession workerSession, Exception ex) {
    Console.WriteLine("worker error");
  }

  public void OnPing(IWorkerSession workerSession) {
    Console.WriteLine("worker ping");
  }

  public void OnLogMessage(IWorkerSession workerSession, LogMessage logMessage) {
    Console.WriteLine("worker log message");
  }
}
