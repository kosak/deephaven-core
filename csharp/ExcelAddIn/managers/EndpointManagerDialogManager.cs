using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.Views;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.ViewModels;
using ExcelAddIn.views;
using static System.Net.Mime.MediaTypeNames;

namespace Deephaven.ExcelAddIn.Managers;

internal class EndpointManagerDialogManager : IObserver<AddOrRemove<EndpointId>>, IDisposable {
  //
  // ConnectionManagerDialog cmDialog,
  // ConcurrentDictionary<ConnectionManagerDialogRow, ConnectionManagerDialogRowManager> rowToManager,
  // StateManager stateManager) 
  public static EndpointManagerDialogManager Create(StateManager stateManager,
    EndpointManagerDialog cmDialog) {
    var result = new EndpointManagerDialogManager(stateManager, cmDialog);
    cmDialog.OnNewButtonClicked += result.OnNewButtonClicked;
    cmDialog.OnDeleteButtonClicked += result.OnDeleteButtonClicked;
    cmDialog.OnReconnectButtonClicked += result.OnReconnectButtonClicked;
    cmDialog.OnMakeDefaultButtonClicked += result.OnMakeDefaultButtonClicked;
    cmDialog.OnEditButtonClicked += result.OnEditButtonClicked;

    var disp = stateManager.SubscribeToEndpointConfigPopulation(result);
    result._disposables.Add(disp);
    return result;
  }

  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly EndpointManagerDialog _cmDialog;
  private readonly Dictionary<EndpointId, EndpointManagerDialogRow> _idToRow = new();
  private readonly Dictionary<EndpointManagerDialogRow, EndpointManagerDialogRowManager> _rowToManager = new();
  private readonly List<IDisposable> _disposables = new();

  public EndpointManagerDialogManager(StateManager stateManager, EndpointManagerDialog cmDialog) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _cmDialog = cmDialog;
  }

  public void OnNext(AddOrRemove<EndpointId> aor) {
    if (_workerThread.EnqueueOrNop(() => OnNext(aor))) {
      return;
    }

    if (aor.IsAdd) {
      var endpointId = aor.Value;
      var row = new EndpointManagerDialogRow(endpointId.Id);
      var statusRowManager = EndpointManagerDialogRowManager.Create(row, endpointId, _stateManager);
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
    if (_workerThread.EnqueueOrNop(Dispose)) {
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
    var cvm = EndpointDialogViewModel.OfEmpty();
    ConfigDialogFactory.CreateAndShow(_stateManager, cvm, null);
  }

  void OnDeleteButtonClicked(EndpointManagerDialogRow[] rows) {
    if (_workerThread.EnqueueOrNop(() => OnDeleteButtonClicked(rows))) {
      return;
    }

    void ShowFailuresIfAny(Dictionary<EndpointId, bool> results) {
      var failureMessages = results.Where(kvp => !kvp.Value)
        .Select(kvp => $"{kvp.Key} still in use")
        .ToArray();
      if (failureMessages.Length == 0) {
        return;
      }
      var failureText = string.Join(Environment.NewLine, failureMessages);
      const string caption = "Couldn't delete some selections";
      _cmDialog.BeginInvoke(() => {
        var mbox = new DeephavenMessageBox(caption, failureText, false);
        _ = mbox.ShowDialog(_cmDialog);
      });
    }

    var managers = rows.Where(_rowToManager.ContainsKey)
      .Select(row => _rowToManager[row])
      .ToArray();

    EndpointManagerDialogRowManager.TryDeleteBatch(managers, ShowFailuresIfAny);
  }

  void OnReconnectButtonClicked(EndpointManagerDialogRow[] rows) {
    if (_workerThread.EnqueueOrNop(() => OnReconnectButtonClicked(rows))) {
      return;
    }

    foreach (var row in rows) {
      if (!_rowToManager.TryGetValue(row, out var manager)) {
        continue;
      }
      manager.DoReconnect();
    }
  }

  void OnMakeDefaultButtonClicked(EndpointManagerDialogRow[] rows) {
    if (_workerThread.EnqueueOrNop(() => OnMakeDefaultButtonClicked(rows))) {
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

  void OnEditButtonClicked(EndpointManagerDialogRow[] rows) {
    if (_workerThread.EnqueueOrNop(() => OnEditButtonClicked(rows))) {
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
