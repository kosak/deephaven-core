using Deephaven.ExcelAddIn.Managers;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Views;

namespace Deephaven.ExcelAddIn.Factories;

internal static class EndpointManagerDialogFactory {
  public static void CreateAndShow(StateManager stateManager) {
    Utility.RunInBackground(() => {
      var cmDialog = new EndpointManagerDialog();
      var dm = EndpointManagerDialogManager.Create(stateManager, cmDialog);
      cmDialog.Closed += (_, _) => dm.Dispose();
      // Blocks forever (in this private thread)
      cmDialog.ShowDialog(); 
    });
  }
}
