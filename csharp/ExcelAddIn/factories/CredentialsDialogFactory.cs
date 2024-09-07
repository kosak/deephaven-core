using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.ViewModels;
using ExcelAddIn.views;

namespace Deephaven.ExcelAddIn.Factories;

internal static class CredentialsDialogFactory {
  public static void CreateAndShow(StateManager stateManager, CredentialsDialogViewModel cvm) {
    Utility.RunInBackground(() => {
      var cd = new CredentialsDialog(cvm);
      var state = new CredentialsDialogState(stateManager, cd, cvm);
      cd.OnSetCredentialsButtonClicked += state.OnSetCredentials;
      cd.OnTestCredentialsButtonClicked += state.OnTestCredentials;
      // Blocks forever
      cd.ShowDialog();
    });
  }
}

internal class CredentialsDialogState(
  StateManager stateManager,
  CredentialsDialog credentialsDialog,
  CredentialsDialogViewModel cvm) {
  private readonly VersionTracker _versionTracker = new();

  public void OnSetCredentials() {
    if (!cvm.TryMakeCredentials(out var newCreds, out var error)) {
      ShowMessageBox(error);
      return;
    }

    stateManager.SetCredentials(newCreds);
    if (cvm.IsDefault) {
      stateManager.SetDefaultEndpointId(newCreds.Id);
    }

    credentialsDialog!.Close();
  }

  public void OnTestCredentials() {
    if (!cvm.TryMakeCredentials(out var newCreds, out var error)) {
      ShowMessageBox(error);
      return;
    }

    credentialsDialog!.SetTestResultsBox("Checking credentials");
    // Check credentials on its own thread
    Utility.RunInBackground(() => TestCredentialsThreadFunc(newCreds));
  }

  private void TestCredentialsThreadFunc(CredentialsBase creds) {
    var latestCookie = _versionTracker.SetNewVersion();

    var state = "OK";
    try {
      // This operation might take some time.
      var temp = SessionBaseFactory.Create(creds, stateManager.WorkerThread);
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

  private void ShowMessageBox(string error) {
    var dhm = new DeephavenMessageBox("Please provide missing fields", error);
    dhm.ShowDialog(credentialsDialog);
  }
}
