using System.Collections.Concurrent;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.Views;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.ViewModels;

namespace Deephaven.ExcelAddIn.Managers;

internal class ConnectionManagerDialogManager : IObserver<AddOrRemove<EndpointId>>, IDisposable {
  //
  // ConnectionManagerDialog cmDialog,
  // ConcurrentDictionary<ConnectionManagerDialogRow, ConnectionManagerDialogRowManager> rowToManager,
  // StateManager stateManager) 
  public static ConnectionManagerDialogManager Create(StateManager stateManager,
    ConnectionManagerDialog cmDialog) {
    var result = new ConnectionManagerDialogManager(stateManager.WorkerThread);
    cmDialog.OnNewButtonClicked += result.OnNewButtonClicked;
    cmDialog.OnDeleteButtonClicked += result.OnDeleteButtonClicked;
    cmDialog.OnReconnectButtonClicked += result.OnReconnectButtonClicked;
    cmDialog.OnMakeDefaultButtonClicked += result.OnMakeDefaultButtonClicked;
    cmDialog.OnEditButtonClicked += result.OnEditButtonClicked;

    var disp = stateManager.SubscribeToCredentialsPopulation(result);
    result._disposables.Add(disp);
    return result;
  }

  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly ConnectionManagerDialog _cmDialog;
  private readonly Dictionary<EndpointId, ConnectionManagerDialogRow> _idToRow = new();
  private readonly Dictionary<ConnectionManagerDialogRow, ConnectionManagerDialogRowManager> _rowToManager = new();
  private readonly List<IDisposable> _disposables = new();

  public ConnectionManagerDialogManager(WorkerThread workerThread) {
    _workerThread = workerThread;
  }

  public void OnNext(AddOrRemove<EndpointId> aor) {
    if (_workerThread.InvokeIfRequired(() => OnNext(aor))) {
      return;
    }

    if (aor.IsAdd) {
      var endpointId = aor.Value;
      var row = new ConnectionManagerDialogRow(endpointId.Id);
      var statusRowManager = ConnectionManagerDialogRowManager.Create(row, endpointId, _stateManager);
      _rowToManager.Add(row, statusRowManager);
      _idToRow.Add(endpointId, row);
      _disposables.Add(statusRowManager);

      _cmDialog.AddRow(row);
      return;
    }

    // Remove!
    if (!_idToRow.Remove(aor.Value, out var rowToDelete) ||
        !_rowToManager.Remove(rowToDelete, out var rowManager)) {
      return;
    }

    _cmDialog.RemoveRow(rowToDelete);
    rowManager.Dispose();
  }

  public void Dispose() {
    if (_workerThread.InvokeIfRequired(Dispose)) {
      return;
    }

    var temp = _disposables.ToArray();
    _disposables.Clear();
    foreach (var disposable in temp) {
      disposable.Dispose();
    }
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  void OnNewButtonClicked() {
    var cvm = CredentialsDialogViewModel.OfEmpty();
    var dialog = CredentialsDialogFactory.Create(sm, cvm);
    dialog.Show();
  }

  void OnDeleteButtonClicked(ConnectionManagerDialogRow[] rows) {
    var rowsLeft = rows.Length;
    var failures = new List<EndpointId>();

    void FinishFunc(EndpointId id) {
      --rowsLeft;
      if (rowsLeft > 0 || failures.Count == 0) {
        return;
      }

      var text = $"ids still in use: {string.Join(", ", failures.Select(f => f.ToString()))}";

      Utility.RunInBackground(() =>
        MessageBox.Show(text, "Couldn't delete all selections", MessageBoxButtons.OK));
    }

    void FailureFunc(EndpointId id, string reason) {
      failures.Add(id);
      FinishFunc(id);
    }

    foreach (var row in rows) {
      if (!rowToManager.TryGetValue(row, out var manager)) {
        continue;
      }

      manager.DoDelete(FinishFunc, FailureFunc);
    }
  }

  void OnReconnectButtonClicked(ConnectionManagerDialogRow[] rows) {
    foreach (var row in rows) {
      if (!rowToManager.TryGetValue(row, out var manager)) {
        continue;
      }
      manager.DoReconnect();
    }
  }


  void OnMakeDefaultButtonClicked(ConnectionManagerDialogRow[] rows) {
    // Make the last selected row the default
    if (rows.Length == 0) {
      return;
    }

    var row = rows[^1];
    if (!rowToManager.TryGetValue(row, out var manager)) {
      return;
    }

    manager.DoSetAsDefault();
  }

  void OnEditButtonClicked(ConnectionManagerDialogRow[] rows) {
    foreach (var row in rows) {
      if (!rowToManager.TryGetValue(row, out var manager)) {
        continue;
      }
      manager.DoEdit();
    }
  }
}
