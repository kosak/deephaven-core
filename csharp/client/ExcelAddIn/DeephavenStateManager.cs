using System.Diagnostics;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class DeephavenStateManager {
  private const string ServerAddress = "10.0.4.106:10000";

  public static readonly DeephavenStateManager Instance = new();

  private readonly Lender<ClientOrStatus> _clientLender = new(1);

  public DeephavenStateManager() {
    _clientLender.Replace(new ClientOrStatus(null, "Not connected to Deephaven"));
  }

  public void Connect() {
    _clientLender.Replace(new ClientOrStatus(null, "Connecting..."));
    Task.Run(ConnectHelper);
  }

  private void ConnectHelper() {
    try {
      var newClient = DeephavenClient.Client.Connect(ServerAddress, new ClientOptions());
      _clientLender.Replace(new ClientOrStatus(newClient, null));
    } catch (Exception ex) {
      _clientLender.Replace(new ClientOrStatus(null, ex.Message));
    }
  }

  public void Reconnect() {

  }

  public IExcelObservable SnapshotTable(string tableName, TableFilter filter) {
    Debug.WriteLine("making another one why");
    return new SnapshotHandler(_clientLender, tableName, filter);
  }
}

public class ClientOrStatus {
  public readonly Client? Client;
  public readonly string? Status;

  public ClientOrStatus(Client? client, string? status) {
    Client = client;
    Status = status;
  }
}
