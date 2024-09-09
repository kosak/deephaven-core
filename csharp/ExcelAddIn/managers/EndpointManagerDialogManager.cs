using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.Views;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.ViewModels;
using ExcelAddIn.views;

namespace Deephaven.ExcelAddIn.Managers;

/// <summary>
/// How to parse the name: this is the Manager for the EndpointManagerDialog.
/// This should not be confused with EndpointManagerDialogRowManager, which is the
/// Manager for the EndpointManagerDialogRow.
/// </summary>
internal class EndpointManagerDialogManager : IValueObserver<SharableDict<EndpointConfigBase>>,
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
      // If there are modifies in the dict, we ignore them.
      // Modifies are handled by the existing EndpointManagerDialogRowManager for that row.
      var (adds, removes, modifies) = _prevDict.CalcDifference(dict);

      Debug.WriteLine($"_prevDict was {_prevDict}");
      Debug.WriteLine($"dict is {dict}");
      Debug.WriteLine($"These are your adds: {adds}");
      Debug.WriteLine($"These are your removes: {removes}");
      Debug.WriteLine($"These are your modified: {modifies}");

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

  void OnNewButtonClicked() {
    var cvm = EndpointDialogViewModel.OfEmpty();
    ConfigDialogFactory.CreateAndShow(_stateManager, cvm, null);
  }

  void OnDeleteButtonClicked(EndpointManagerDialogRow[] rows) {
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

  void OnReconnectButtonClicked(EndpointManagerDialogRow[] rows) {
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
  void OnMakeDefaultButtonClicked(EndpointManagerDialogRow[] rows) {
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

  void OnEditButtonClicked(EndpointManagerDialogRow[] rows) {
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
