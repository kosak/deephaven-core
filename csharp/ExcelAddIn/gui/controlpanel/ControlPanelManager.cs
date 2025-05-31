using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Gui;

internal class ControlPanelManager : IDisposable {
  /// <summary>
  /// Used by EnsureShown to make sure at least one dialog is visible
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
      var cpDialog = new ControlPanel();
      var cpManager = Create(stateManager, cpDialog);
      cpDialog.Closed += (_, _) => cpManager.Dispose();
      // Blocks forever (in this dedicated thread) until the form is closed.
      cpDialog.ShowDialog();
      Interlocked.Decrement(ref _numOpenDialogs);
    });
  }

  private static ControlPanelManager Create(StateManager stateManager,
    ControlPanel cpDialog) {
    var em = EndpointManager.Create(stateManager, cpDialog.Endpoint);
    var sm = StatusManager.Create(stateManager, cpDialog.Status);
    var result = new ControlPanelManager(stateManager, cpDialog, em, sm);
    return result;
  }
}
