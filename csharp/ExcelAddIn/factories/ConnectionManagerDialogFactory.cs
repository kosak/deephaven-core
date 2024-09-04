using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.ViewModels;
using Deephaven.ExcelAddIn.Views;
using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;

namespace Deephaven.ExcelAddIn.Factories;

internal static class ConnectionManagerDialogFactory {
  public static void CreateAndShow(StateManager sm) {
    // The "new" button creates a "New/Edit Credentials" dialog
    void OnNewButtonClicked() {
      var cvm = CredentialsDialogViewModel.OfEmpty();
      var dialog = CredentialsDialogFactory.Create(sm, cvm);
      dialog.Show();
    }

    var cmDialog = new ConnectionManagerDialog(OnNewButtonClicked);
    cmDialog.Show();
    var cmso = new ConnectionManagerSessionObserver(sm, cmDialog);
    var disposer = sm.SubscribeToSessions(cmso);

    cmDialog.Closed += (_, _) => {
      disposer.Dispose();
      cmso.Dispose();
    };
  }
}


