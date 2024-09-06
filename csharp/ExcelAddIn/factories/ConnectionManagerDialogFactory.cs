using System.Collections.Concurrent;
using Deephaven.ExcelAddIn.Managers;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.ViewModels;
using Deephaven.ExcelAddIn.Views;

namespace Deephaven.ExcelAddIn.Factories;

internal static class ConnectionManagerDialogFactory {
  public static void CreateAndShow(StateManager stateManager) {
    var cmDialog = new ConnectionManagerDialog();
    cmDialog.Show();
    var dm = ConnectionManagerDialogManager.Create(stateManager, cmDialog);
    cmDialog.Closed += (_, _) => dm.Dispose();
  }
}

internal class ConnectionManagerDialogState {
  // The "new" button creates a "New/Edit Credentials" dialog



  
  var rowToManager = new ConcurrentDictionary<ConnectionManagerDialogRow, ConnectionManagerDialogRowManager>();


}


