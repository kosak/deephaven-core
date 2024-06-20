using System.Diagnostics;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class DeephavenStateManager {
  private const string ServerAddress = "10.0.4.60:10000";

  public static readonly DeephavenStateManager Instance = new();

  private readonly Lender<Client> _clientLender = new(1);

  public void Connect() {
    _clientLender.Replace(null);
    Task.Run(ConnectHelper);
  }

  private void ConnectHelper() {
    try {
      var newClient = DeephavenClient.Client.Connect(ServerAddress, new ClientOptions());
      _clientLender.Replace(newClient);
    } catch (Exception ex) {
      Debug.WriteLine("uh oh");
      Debug.WriteLine(ex);
    }
  }

  public void Reconnect() {

  }

  public IExcelObservable SnapshotTable(string tableName, TableFilter filter) {
    Debug.WriteLine("making another one why");
    return new SnapshotHandler(_clientLender, tableName, filter);
  }
}
