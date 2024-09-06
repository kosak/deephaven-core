using Deephaven.ExcelAddIn.Managers;
using Deephaven.ExcelAddIn.Views;

namespace Deephaven.ExcelAddIn.Factories;

internal static class ConnectionManagerDialogFactory {
  public static void CreateAndShow(StateManager stateManager, Control guiThreadRepresentative) {
    if (guiThreadRepresentative.InvokeRequired) {
      guiThreadRepresentative.Invoke(() => CreateAndShow(stateManager, guiThreadRepresentative));
      return;
    }
    var cmDialog = new ConnectionManagerDialog();
    cmDialog.Show();
    var dm = ConnectionManagerDialogManager.Create(stateManager, cmDialog);
    cmDialog.Closed += (_, _) => dm.Dispose();
  }
}
