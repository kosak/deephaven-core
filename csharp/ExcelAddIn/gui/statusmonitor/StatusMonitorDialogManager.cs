using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Gui;

public class StatusMonitorDialogManager : 
    IValueObserver<SharableDict<OpStatus>>,
    IDisposable {
  /// <summary>
  /// Used by EnsureDialogShown to make sure at least one dialog is visible
  /// </summary>
  private static long _numOpenDialogs = 0;

  public static void EnsureDialogShown(StateManager stateManager) {
    // Increment the variable temporarily to avoid a race
    var temp = Interlocked.Increment(ref _numOpenDialogs);
    if (temp == 1) {
      CreateAndShow(stateManager);
    }
    Interlocked.Decrement(ref _numOpenDialogs);
  }

  public static void CreateAndShow(StateManager stateManager) {
    Interlocked.Increment(ref _numOpenDialogs);
    Background.Run(() => {
      var smDialog = new StatusMonitorDialog();
      var sm = Create(stateManager, smDialog);
      smDialog.Closed += (_, _) => sm.Dispose();
      // Blocks forever (in this dedicated thread) until the form is closed.
      smDialog.ShowDialog();
      Interlocked.Decrement(ref _numOpenDialogs);
    });
  }

  private static StatusMonitorDialogManager Create(StateManager stateManager,
    StatusMonitorDialog smDialog) {
    var result = new StatusMonitorDialogManager(stateManager, smDialog);
    result._upstreamSubsription = stateManager.SubscribeToStatusDict(result);
    smDialog.OnRetryButtonClicked += result.OnRetryButtonClicked;
    return result;
  }

  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private readonly StatusMonitorDialog _smDialog;
  private readonly Dictionary<Int64, StatusMonitorDialogRow> _idToRow = new();
  private IDisposable? _upstreamSubsription = null;
  private SharableDict<OpStatus> _prevDict = SharableDict<OpStatus>.Empty;

  public StatusMonitorDialogManager(StateManager stateManager, StatusMonitorDialog smDialog) {
    _stateManager = stateManager;
    _smDialog = smDialog;
  }

  public void Dispose() {
    lock (_sync) {
      if (Utility.Exchange(ref _isDisposed, true)) {
        return;
      }

      Utility.ClearAndDispose(ref _upstreamSubsription);
    }
  }

  public void OnNext(SharableDict<OpStatus> dict) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }

      // If there are modifies in the dict, we ignore them.
      // Modifies are handled by the existing EndpointManagerDialogRowManager for that row.
      var (adds, removes, modifies) = _prevDict.CalcDifference(dict);

      _prevDict = dict;

      foreach (var kvp in adds) {
        var row = new StatusMonitorDialogRow(kvp.Value);
        _idToRow.Add(kvp.Key, row);
        _smDialog.AddRow(row);
      }

      foreach (var kvp in removes) {
        if (!_idToRow.Remove(kvp.Key, out var row)) {
          continue;
        }

        _smDialog.RemoveRow(row);
      }

      foreach (var kvp in modifies) {
        if (!_idToRow.TryGetValue(kvp.Key, out var row)) {
          continue;
        }

        row.SetValue(kvp.Value);
      }
    }
  }

  private void OnRetryButtonClicked(StatusMonitorDialogRow[] rows) {
    Action[] actions;
    lock (_sync) {
      actions = rows.Select(row => row.OpStatus.RetryAction).ToArray();
    }
    foreach (var action in actions) {
      action.Invoke();
    }
  }
}
