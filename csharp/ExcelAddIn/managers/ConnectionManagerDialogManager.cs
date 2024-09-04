using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.Views;
using System.Diagnostics;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Managers;

internal class ConnectionManagerDialogManager(
  ConnectionManagerDialog cmDialog,
  StateManager stateManager) : IObserver<AddOrRemove<EndpointId>>, IDisposable {
  private readonly WorkerThread _workerThread = stateManager.WorkerThread;
  private readonly List<IDisposable> _disposables = new();

  public void OnNext(AddOrRemove<EndpointId> aor) {
    if (_workerThread.InvokeIfRequired(() => OnNext(aor))) {
      return;
    }

    if (!aor.IsAdd) {
      // TODO(kosak)
      Debug.WriteLine("Remove is not handled");
      return;
    }

    var endpointId = aor.Value;
    var row = new ConnectionManagerDialogRow(endpointId.Id);
    var statusRowManager = ConnectionManagerDialogRowManager.Create(row, endpointId, stateManager);
    _disposables.Add(statusRowManager);

    cmDialog.AddRow(row);
  }

  public void Dispose() {
    // Since the GUI thread is where we added these disposables, the GUI thread is where we will
    // access and dispose them.
    cmDialog.Invoke(() => {
      var temp = _disposables.ToArray();
      _disposables.Clear();
      foreach (var disposable in temp) {
        disposable.Dispose();
      }
    });
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }
}
