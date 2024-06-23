using System.Diagnostics;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class DeephavenStateManager {
  private const string ServerAddress = "10.0.4.106:10000";

  public static readonly DeephavenStateManager Instance = new();

  private readonly Lender<ClientOrStatus> _clientLender;
  private readonly Notifier<Unit> _notifier = new();

  public DeephavenStateManager() {
    _clientLender = new (1, ClientOrStatus.Of("Not connected to Deephaven"));
  }

  public void Connect() {
    SetStatus("Connecting...");
    Task.Run(ConnectHelper);
  }

  private void ConnectHelper() {
    try {
      var newClient = DeephavenClient.Client.Connect(ServerAddress, new ClientOptions());
      SetClient(newClient);
    } catch (Exception ex) {
      SetStatus(ex.Message);
    }
  }

  public void Reconnect() {

  }

  public IExcelObservable SnapshotTable(string tableName, TableFilter filter) {
    Debug.WriteLine("making another one why");
    var sh = new SnapshotHandler(_clientLender, tableName, filter);
    return new DeephavenHandler(_notifier, sh);
  }

  public IExcelObservable SubscribeToTable(string tableName, TableFilter filter) {
    Debug.WriteLine("lalalalalalaal making another one why");
    var sh = new SubscribeHandler(_clientLender, tableName, filter);
    return new DeephavenHandler(_notifier, sh);
  }

  private void SetStatus(string message) {
    _clientLender.Replace(ClientOrStatus.Of(message));
    _notifier.NotifyAll(new Unit());
  }

  private void SetClient(Client client) {
    _clientLender.Replace(ClientOrStatus.Of(client));
    _notifier.NotifyAll(new Unit());
  }
}

public class ClientOrStatus {
  public readonly Client? Client;
  public readonly string? Status;

  public static ClientOrStatus Of(Client client) => new (client, null);
  public static ClientOrStatus Of(string status) => new (null, status);

  private ClientOrStatus(Client? client, string? status) {
    Client = client;
    Status = status;
  }
}
