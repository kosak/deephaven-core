using System.Collections.Concurrent;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.Views;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.ViewModels;
using ExcelAddIn.views;

namespace Deephaven.ExcelAddIn.Managers;

internal class ConnectionManagerDialogManager : IObserver<AddOrRemove<EndpointId>>, IDisposable {
  //
  // ConnectionManagerDialog cmDialog,
  // ConcurrentDictionary<ConnectionManagerDialogRow, ConnectionManagerDialogRowManager> rowToManager,
  // StateManager stateManager) 
  public static ConnectionManagerDialogManager Create(StateManager stateManager,
    ConnectionManagerDialog cmDialog) {
    var result = new ConnectionManagerDialogManager(stateManager, cmDialog);
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

  public ConnectionManagerDialogManager(StateManager stateManager, ConnectionManagerDialog cmDialog) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _cmDialog = cmDialog;
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
    var dialog = CredentialsDialogFactory.Create(_stateManager, cvm);
    dialog.Show();
  }

  private class FailureCollector {
    private readonly ConnectionManagerDialog _cmDialog;
    private readonly object _sync = new();
    private int _rowsLeft = 0;
    private List<EndpointId> _failures = new();

    public FailureCollector(ConnectionManagerDialog cmDialog) {
      _cmDialog = cmDialog;
    }

    public void OnFailure(EndpointId id, string reason) {
      lock (_sync) {
        _failures.Add(id);
      }

      FinalSteps();
    }

    public void OnSuccess(EndpointId id) {
      FinalSteps();
    }

    private void FinalSteps() {
      string text;
      lock (_sync) {
        --_rowsLeft;
        if (_rowsLeft > 0 || _failures.Count == 0) {
          return;
        }

        text = $"ids still in use: {string.Join(", ", _failures.Select(f => f.ToString()))}";
      }

      const string caption = "Couldn't delete all selections";
      var mbox = new DeephavenMessageBox(caption, text);
      mbox.ShowDialog(_cmDialog);
    }
  }

  void OnDeleteButtonClicked(ConnectionManagerDialogRow[] rows) {
    if (_workerThread.InvokeIfRequired(() => OnDeleteButtonClicked(rows))) {
      return;
    }

    var fc = new FailureCollector(_cmDialog);
    foreach (var row in rows) {
      if (!_rowToManager.TryGetValue(row, out var manager)) {
        continue;
      }
      manager.DoDelete(fc.OnSuccess, fc.OnFailure);
    }
  }

  void OnReconnectButtonClicked(ConnectionManagerDialogRow[] rows) {
    if (_workerThread.InvokeIfRequired(() => OnReconnectButtonClicked(rows))) {
      return;
    }

    foreach (var row in rows) {
      if (!_rowToManager.TryGetValue(row, out var manager)) {
        continue;
      }
      manager.DoReconnect();
    }
  }

  void OnMakeDefaultButtonClicked(ConnectionManagerDialogRow[] rows) {
    if (_workerThread.InvokeIfRequired(() => OnMakeDefaultButtonClicked(rows))) {
      return;
    }

    // Make the last selected row the default
    if (rows.Length == 0) {
      return;
    }

    var row = rows[^1];
    if (!_rowToManager.TryGetValue(row, out var manager)) {
      return;
    }

    manager.DoSetAsDefault();
  }

  void OnEditButtonClicked(ConnectionManagerDialogRow[] rows) {
    if (_workerThread.InvokeIfRequired(() => OnMakeDefaultButtonClicked(rows))) {
      return;
    }

    foreach (var row in rows) {
      if (!_rowToManager.TryGetValue(row, out var manager)) {
        continue;
      }
      manager.DoEdit();
    }
  }
}
