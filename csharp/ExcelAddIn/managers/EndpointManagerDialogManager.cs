using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.Views;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.ViewModels;
using ExcelAddIn.views;

namespace Deephaven.ExcelAddIn.Managers;

internal class EndpointManagerDialogManager : IObserver<SharableDict<EndpointConfigBase>>,
  IDisposable {
  public static EndpointManagerDialogManager Create(StateManager stateManager,
    EndpointManagerDialog cmDialog) {
    var result = new EndpointManagerDialogManager(stateManager, cmDialog);
    cmDialog.OnNewButtonClicked += result.OnNewButtonClicked;
    cmDialog.OnDeleteButtonClicked += result.OnDeleteButtonClicked;
    cmDialog.OnReconnectButtonClicked += result.OnReconnectButtonClicked;
    cmDialog.OnMakeDefaultButtonClicked += result.OnMakeDefaultButtonClicked;
    cmDialog.OnEditButtonClicked += result.OnEditButtonClicked;

    result._upstreamSubsription = stateManager.SubscribeToEndpointDict(result);
    return result;
  }

  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private readonly Latch _isDisposed = new();
  private readonly EndpointManagerDialog _cmDialog;
  private readonly Dictionary<EndpointId, EndpointManagerDialogRow> _idToRow = new();
  private readonly Dictionary<EndpointManagerDialogRow, EndpointManagerDialogRowManager> _rowToManager = new();
  private IDisposable? _upstreamSubsription = null;
  private SharableDict<EndpointConfigBase> _prevDict = SharableDict<EndpointConfigBase>.Empty;

  public EndpointManagerDialogManager(StateManager stateManager, EndpointManagerDialog cmDialog) {
    _stateManager = stateManager;
    _cmDialog = cmDialog;
  }

  public void OnNext(SharableDict<EndpointConfigBase> dict) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      var (adds, removes, modifies) = _prevDict.CalcDifference(dict);
      _prevDict = dict;

      foreach (var item in adds.Values) {
        var endpointId = item.Id;
        var row = new EndpointManagerDialogRow(endpointId.Id);
        var statusRowManager = EndpointManagerDialogRowManager.Create(row, endpointId, _stateManager);
        _rowToManager.Add(row, statusRowManager);
        _idToRow.Add(endpointId, row);
        _cmDialog.AddRow(row);
      }

      foreach (var item in removes.Values) {
        if (!_idToRow.Remove(item.Id, out var rowToDelete) ||
            !_rowToManager.Remove(rowToDelete, out var rowManager)) {
          continue;
        }

        _cmDialog.RemoveRow(rowToDelete);
        rowManager.Dispose();
      }

      foreach (var item in modifies.Values) {
        // TODO(kosak)
        Debug.WriteLine("what to do about modifies??");
      }
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      var temp = _rowToManager.Values.ToArray();
      _idToRow.Clear();
      _rowToManager.Clear();

      Utility.ClearAndDispose(ref _upstreamSubsription);
      foreach (var disposable in temp) {
        disposable.Dispose();
      }
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

    var failures = new List<string>();
    lock (_sync) {
      var managers = rows.Where(_rowToManager.ContainsKey)
        .Select(row => _rowToManager[row])
        .ToArray();

      foreach (var manager in managers) {
        if (!manager.TryDelete()) {
          failures.Add(manager.EndpointWhatever);
        }
      }
    }
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
