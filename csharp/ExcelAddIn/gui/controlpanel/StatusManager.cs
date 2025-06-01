using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Gui;

public class StatusManager : IValueObserver<SharableDict<OpStatus>>,
    IDisposable {

  public static StatusManager Create(StateManager stateManager,
    StatusElements statusElements) {
    var result = new StatusManager(stateManager, statusElements);
    result._upstreamSubsription = stateManager.SubscribeToStatusDict(result);
    statusElements.OnRetryButtonClicked += result.OnRetryButtonClicked;
    return result;
  }

  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private readonly StatusElements _statusElements;
  private readonly Dictionary<Int64, StatusMonitorRow> _idToRow = new();
  private IDisposable? _upstreamSubsription = null;
  private SharableDict<OpStatus> _prevDict = SharableDict<OpStatus>.Empty;

  private StatusManager(StateManager stateManager, StatusElements statusElements) {
    _stateManager = stateManager;
    _statusElements = statusElements;
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
        var row = new StatusMonitorRow(kvp.Value);
        _idToRow.Add(kvp.Key, row);
        _statusElements.AddRow(row);
      }

      foreach (var kvp in removes) {
        if (!_idToRow.Remove(kvp.Key, out var row)) {
          continue;
        }

        _statusElements.RemoveRow(row);
      }

      foreach (var kvp in modifies) {
        if (!_idToRow.TryGetValue(kvp.Key, out var row)) {
          continue;
        }

        row.SetValue(kvp.Value);
      }
    }
  }

  private void OnRetryButtonClicked(StatusMonitorRow[] rows) {
    Action[] actions;
    lock (_sync) {
      actions = rows.Select(row => row.OpStatus.RetryAction).ToArray();
    }
    foreach (var action in actions) {
      action.Invoke();
    }
  }
}
