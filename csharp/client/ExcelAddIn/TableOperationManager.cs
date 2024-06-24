using ExcelDna.Integration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal sealed class TableOperationManager {
  private ClientOrStatus _clientOrStatus = ClientOrStatus.Of("Not connected to Deephaven");
  private readonly object _sync = new();
  private readonly HashSet<IDeephavenTableOperation> _tableOperations = new();

  public void Register(IDeephavenTableOperation tableOperation) {
    Invoke(() => {
      _tableOperations.Add(tableOperation);
      tableOperation.Start(_clientOrStatus);
    });
  }

  public void Unregister(IDeephavenTableOperation tableOperation) {
    Invoke(() => {
      _tableOperations.Remove(tableOperation);
      tableOperation.Stop();
    });
  }

  public void Connect(string connectionString) {
    Invoke(() => ConnectHelper(connectionString));
  }

  private void ConnectHelper(string connectionString) {
    try {
      var newClient = DeephavenClient.Client.Connect(connectionString, new ClientOptions());
      _clientOrStatus = ClientOrStatus.Of(newClient);
    } catch (Exception ex) {
      _clientOrStatus = ClientOrStatus.Of(ex.Message);
    }

    foreach (var top in _tableOperations) {
      // try-catch
      top.Stop();
    }
    foreach (var top in _tableOperations) {
      // try-catch
      top.Start(_clientOrStatus, statusObserverDoNotWant);
    }
  }
}
