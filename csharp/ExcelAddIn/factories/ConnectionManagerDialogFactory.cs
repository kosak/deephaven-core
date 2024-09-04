using System.Diagnostics;
using Deephaven.ExcelAddIn.Managers;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.ViewModels;
using Deephaven.ExcelAddIn.Views;

namespace Deephaven.ExcelAddIn.Factories;

internal static class ConnectionManagerDialogFactory {
  public static void CreateAndShow(StateManager sm) {
    // The "new" button creates a "New/Edit Credentials" dialog
    void OnNewButtonClicked() {
      var cvm = CredentialsDialogViewModel.OfEmpty();
      var dialog = CredentialsDialogFactory.Create(sm, cvm);
      dialog.Show();
    }

    void OnDeleteButtonClicked(ConnectionManagerDialogRow[] rows) {
      Debug.WriteLine("would be nice");
    }

    void OnReconnectButtonClicked(ConnectionManagerDialogRow[] rows) {
      Debug.WriteLine("would be nice");
    }

    void OnEditButtonClicked(ConnectionManagerDialogRow[] rows) {
      Debug.WriteLine("would be nice");
    }

    var cmDialog = new ConnectionManagerDialog(OnNewButtonClicked, OnDeleteButtonClicked,
      OnReconnectButtonClicked, OnEditButtonClicked);
    cmDialog.Show();
    var newThing = new ConnectionManagerDialogManager(cmDialog, sm);
    var disposer = sm.SubscribeToSessions(newThing);

    cmDialog.Closed += (_, _) => {
      disposer.Dispose();
      newThing.Dispose();
    };
  }
}


