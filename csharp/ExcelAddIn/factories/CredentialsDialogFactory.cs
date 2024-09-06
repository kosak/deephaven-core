using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.ViewModels;
using ExcelAddIn.views;

namespace Deephaven.ExcelAddIn.Factories;

internal static class CredentialsDialogFactory {
  public static CredentialsDialog Create(StateManager sm, CredentialsDialogViewModel cvm) {
    CredentialsDialog? credentialsDialog = null;

    void OnSetCredentialsButtonClicked() {
      if (!cvm.TryMakeCredentials(out var newCreds, out var error)) {
        ShowMessageBox(error);
        return;
      }

      sm.SetCredentials(newCreds);
      if (cvm.IsDefault) {
        sm.SetDefaultCredentials(newCreds);
      }

      credentialsDialog!.Close();
    }

    var versionTracker = new VersionTracker();

    void TestCredentials(CredentialsBase creds) {
      var latestCookie = versionTracker.SetNewVersion();

      var state = "OK";
      try {
        // This operation might take some time.
        var temp = SessionBaseFactory.Create(creds, sm.WorkerThread);
        temp.Dispose();
      } catch (Exception ex) {
        state = ex.Message;
      }

      if (!latestCookie.IsCurrent) {
        // Our results are moot. Dispose of them.
        return;
      }

      // Our results are valid. Keep them and tell everyone about it.
      credentialsDialog!.SetTestResultsBox(state);
    }

    void OnTestCredentialsButtonClicked() {
      if (!cvm.TryMakeCredentials(out var newCreds, out var error)) {
        ShowMessageBox(error);
        return;
      }

      credentialsDialog!.SetTestResultsBox("Checking credentials");
      // Check credentials on its own thread
      Utility.RunInBackground666(() => TestCredentials(newCreds));
    }

    // Save in captured variable so that the lambdas can access it.
    credentialsDialog = new CredentialsDialog(cvm, OnSetCredentialsButtonClicked, OnTestCredentialsButtonClicked);
    return credentialsDialog;
  }

  private static void ShowMessageBox(string error) {
    MessageBox.Show(error, "Please provide missing fields", MessageBoxButtons.OK);
  }
}
