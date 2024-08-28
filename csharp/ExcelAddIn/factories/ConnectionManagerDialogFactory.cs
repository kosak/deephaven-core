using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.ViewModels;
using Deephaven.ExcelAddIn.Views;
using System.Diagnostics;

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
    var mso = new ConnectionManagerSessionObserver(sm, cmDialog);
    var disposer = sm.SubscribeToSessions(mso);

    cmDialog.Closed += (_, _) => disposer.Dispose();
  }
}

internal class ConnectionManagerSessionObserver(StateManager stateManager,
  ConnectionManagerDialog cmDialog) : IObserver<AddOrRemove<EndpointId>> {

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnNext(AddOrRemove<EndpointId> aor) {
    if (!aor.IsAdd) {
      // TODO(kosak)
      Debug.WriteLine("Remove is not handled");
      return;
    }

    // Add a row to the form
    // Wire up the OnClick button for that row
    // subscribe to the 

    var statusRow = new ConnectionManagerDialogRow(aor.Value.HumanReadableString, stateManager);
    // TODO(kosak): what now
    var subPainDisposable = stateManager.SubscribeToSession(aor.Value, statusRow);
    var subPainDisposable2 = stateManager.SubscribeToCredentials(aor.Value, statusRow);

    // Not sure what the deal is with threading and BindingSource,
    // so I'll Invoke it to get this change on the GUI thread.
    cmDialog.Invoke(() => {
      cmDialog.AddRow(statusRow);
    });
  }
}
