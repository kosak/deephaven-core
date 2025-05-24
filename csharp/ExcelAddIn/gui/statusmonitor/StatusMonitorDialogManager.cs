using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using System.Diagnostics;

namespace Deephaven.ExcelAddIn.Gui;

public class StatusMonitorDialogManager : 
    IValueObserver<SharableDict<OpStatus>>,
    IDisposable {
  public static void CreateAndShow(StateManager stateManager) {
    Background.Run(() => {
      var smDialog = new StatusMonitorDialog();
      var sm = Create(stateManager, smDialog);
      smDialog.Closed += (_, _) => sm.Dispose();
      // Blocks forever (in this dedicated thread) until the form is closed.
      smDialog.ShowDialog();
    });
  }

  private static StatusMonitorDialogManager Create(StateManager stateManager,
    StatusMonitorDialog smDialog) {
    var result = new StatusMonitorDialogManager(stateManager, smDialog);
    result._upstreamSubsription = stateManager.SubscribeToStatusDict(result);
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

      Debug.WriteLine($"_prevDict was {_prevDict}");
      Debug.WriteLine($"dict is {dict}");
      Debug.WriteLine($"These are your adds: {adds}");
      Debug.WriteLine($"These are your removes: {removes}");
      Debug.WriteLine($"These are your modifies: {modifies}");

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
        if (!_idToRow.Remove(kvp.Key, out var row)) {
          continue;
        }

        row.SetValue(kvp.Value);
      }
    }
  }
}
