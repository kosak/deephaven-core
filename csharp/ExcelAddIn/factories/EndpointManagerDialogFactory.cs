using Deephaven.ExcelAddIn.Managers;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Views;

namespace Deephaven.ExcelAddIn.Factories;

internal static class EndpointManagerDialogFactory {
  public static void CreateAndShow(StateManager stateManager) {
    Background.Run(() => {
      var cmDialog = new EndpointManagerDialog();
      var dm = EndpointManagerDialogManager.Create(stateManager, cmDialog);
      cmDialog.Closed += (_, _) => dm.Dispose();
      // Blocks forever (in this dedicated thread) until the form is closed.
      cmDialog.ShowDialog(); 
    });

    Background.Run(() => {
      var qqDialog = new StatusMonitorDialog();
      qqDialog.ShowDialog();
    });
  }
}
