using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal abstract class DeephavenHandler : IExcelObservable {
  private readonly object _sync = new();
  private readonly HashSet<IExcelObserver> _observers = new();

  public IDisposable Subscribe(IExcelObserver observer) {
    lock (_sync) {
      _observers.Add(observer);
    }
    ConnectOrReconnect();
  }

  private protected void DisplayStatusMessage(string message) {
    var matrix = new object[1, 1];
    matrix[0, 0] = message;
    DisplayResult(matrix);
  }

  private protected void DisplayException(Exception ex) {
    DisplayStatusMessage(ex.Message);
  }

  private protected void DisplayResult(object?[,] matrix) {
    foreach (var observer in GetObservers()) {
      observer.OnNext(matrix);
    }
  }

  private IExcelObserver[] GetObservers() {
    lock (_sync) {
      return _observers.ToArray();
    }
  }

  private protected abstract void ConnectOrReconnect(Client client);
}

internal class SnapshotHandler : DeephavenHandler {
  private readonly string _tableName;

  private protected override void ConnectOrReconnect(Client client) {
    DisplayStatusMessage($"Snaphotting \"{_tableName}\"");

    Task.Run(() => PerformFetchTable(client));
  }

  private void PerformFetchTable(Client client) {
    try {
      using var th = client.Manager.FetchTable(_tableName);
      using var ct = th.ToClientTable();
      // TODO(kosak): Filter the client table here
      var result = Renderer.Render(ct);
      DisplayResult(result);
    } catch (Exception ex) {
      DisplayException(ex);
    }
  }
}
