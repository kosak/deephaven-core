using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using System.Diagnostics;

namespace Deephaven.ExcelAddIn.Gui;

public class StatusMonitorDialogManager : 
    IValueObserver<SharableDict<OpStatus>>,
    IDisposable {
  public static void CreateAndShow(StateManager stateManager) {
    Background.Run(() => {
      var smDialog = new EndpointManagerDialog();
      var sm = Create(stateManager, smDialog);
      smDialog.Closed += (_, _) => sm.Dispose();
      // Blocks forever (in this dedicated thread) until the form is closed.
      smDialog.ShowDialog();
    });
  }

  private static StatusMonitorDialogManager Create(StateManager stateManager,
    StatusMonitorDialog smDialog) {
    var result = new StatusMonitorDialogManager(stateManager, smDialog);
    result._upstreamSubsription = stateManager.SubscribeToEndpointDict(result);
    return result;
  }

  private readonly object _sync = new();
  private bool _isDisposed = false;
  private readonly StatusMonitorDialog _smDialog;
  private SharableDict<OpStatus> _prevDict = SharableDict<OpStatus>.Empty;

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
}
