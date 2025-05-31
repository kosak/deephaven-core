using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Gui;

internal class EndpointManager :
  IValueObserver<SharableDict<EndpointConfigBase>>,
  IDisposable {
  public static EndpointManager Create(StateManager stateManager,
    ControlPanel.EndpointElements endpointElements) {
    var result = new EndpointManager(stateManager, endpointElements);
    endpointElements.OnNewButtonClicked += result.OnNewButtonClicked;
    endpointElements.OnDeleteButtonClicked += result.OnDeleteButtonClicked;
    endpointElements.OnReconnectButtonClicked += result.OnReconnectButtonClicked;
    endpointElements.OnMakeDefaultButtonClicked += result.OnMakeDefaultButtonClicked;
    endpointElements.OnEditButtonClicked += result.OnEditButtonClicked;

    result._upstreamSubsription = stateManager.SubscribeToEndpointDict(result);
    return result;
  }

  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private readonly ControlPanel.EndpointElements _endpointElements;
  private readonly Dictionary<EndpointId, EndpointManagerDialogRow> _idToRow = new();
  private readonly Dictionary<EndpointManagerDialogRow, EndpointManagerDialogRowManager> _rowToManager = new();
  private IDisposable? _upstreamSubsription = null;
  private SharableDict<EndpointConfigBase> _prevDict = SharableDict<EndpointConfigBase>.Empty;

  private EndpointManager(StateManager stateManager,
    ControlPanel.EndpointElements endpointElements) {
    _stateManager = stateManager;
    _endpointElements = endpointElements;
  }

  public void Dispose() {
    lock (_sync) {
      if (Utility.Exchange(ref _isDisposed, true)) {
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

  public void OnNext(SharableDict<EndpointConfigBase> dict) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }

      // If there are modifies in the dict, we ignore them.
      // Modifies are handled by the existing EndpointManagerDialogRowManager for that row.
      var (adds, removes, modifies) = _prevDict.CalcDifference(dict);

      _prevDict = dict;

      foreach (var item in adds.Values) {
        var endpointId = item.Id;
        var row = new EndpointManagerDialogRow(endpointId.Id);
        var statusRowManager = EndpointManagerDialogRowManager.Create(row, endpointId, _stateManager);
        _rowToManager.Add(row, statusRowManager);
        _idToRow.Add(endpointId, row);
        _endpointElements.AddRow(row);
      }

      foreach (var item in removes.Values) {
        if (!_idToRow.Remove(item.Id, out var rowToDelete) ||
            !_rowToManager.Remove(rowToDelete, out var rowManager)) {
          continue;
        }

        _endpointElements.RemoveRow(rowToDelete);
        rowManager.Dispose();
      }
    }
  }

  private void OnNewButtonClicked() {
    var cvm = ConfigDialogViewModel.OfEmpty();
    ConfigDialogFactory.CreateAndShow(_stateManager, cvm, null);
  }

  private void OnDeleteButtonClicked(EndpointManagerDialogRow[] rows) {
    var failures = new List<string>();
    lock (_sync) {
      var managers = rows.Where(_rowToManager.ContainsKey)
        .Select(row => _rowToManager[row])
        .ToArray();

      foreach (var manager in managers) {
        if (!manager.TryDelete()) {
          failures.Add(manager.EndpointId.Id);
        }
      }
    }

    if (failures.Count == 0) {
      return;
    }
    var failureText = string.Join(Environment.NewLine, failures);
    const string caption = "Couldn't delete some selections";
    _cmDialog.BeginInvoke(() => {
      var mbox = new DeephavenMessageBox(caption, failureText, false);
      _ = mbox.ShowDialog(_cmDialog);
    });
  }

  private void OnReconnectButtonClicked(EndpointManagerDialogRow[] rows) {
    lock (_sync) {
      foreach (var row in rows) {
        if (!_rowToManager.TryGetValue(row, out var manager)) {
          continue;
        }
        manager.DoReconnect();
      }
    }
  }

  /// <summary>
  /// Sets the default endpoint. In the case where there are multiple selected rows,
  /// tries to do the reasonable thing.
  /// </summary>
  /// <param name="rows"></param>
  private void OnMakeDefaultButtonClicked(EndpointManagerDialogRow[] rows) {
    lock (_sync) {
      if (rows.Length == 0) {
        // If no rows are selected, do nothing.
        return;
      }

      // If one or more rows are selected, arbitrarily choose the last one.
      var row = rows[^1];
      if (!_rowToManager.TryGetValue(row, out var manager)) {
        return;
      }

      manager.DoSetAsDefault();
    }
  }

  private void OnEditButtonClicked(EndpointManagerDialogRow[] rows) {
    lock (_sync) {
      foreach (var row in rows) {
        if (!_rowToManager.TryGetValue(row, out var manager)) {
          continue;
        }
        manager.DoEdit();
      }
    }
  }
}
