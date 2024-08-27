using Deephaven.ExcelAddIn.Providers;
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

    var cmDialog = new ConnectionManagerDialog(OnNewButtonClicked);
    cmDialog.Show();
    var mso = new MySessionObserver(sm, cmDialog);
    var disposer = sm.SubscribeToSessions(mso);

    cmDialog.Closed += (_, _) => disposer.Dispose();
  }
}
