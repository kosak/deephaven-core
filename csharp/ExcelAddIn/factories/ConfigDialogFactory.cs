using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.ViewModels;
using ExcelAddIn.views;

namespace Deephaven.ExcelAddIn.Factories;

internal static class ConfigDialogFactory {
  public static void CreateAndShow(StateManager stateManager, EndpointDialogViewModel cvm,
    EndpointId? whitelistId) {
    Background.Run(() => {
      var cd = new ConfigDialog(cvm);
      var state = new EndpointConfigDialogState(stateManager, cd, cvm, whitelistId);

      cd.OnSetCredentialsButtonClicked += state.OnSetCredentials;
      cd.OnTestCredentialsButtonClicked += state.OnTestCredentials;

      cd.Closed += (_, _) => state.Dispose();
      // Blocks (in this private thread) until the dialog is closed.
      cd.ShowDialog();
    });
  }
}

internal class EndpointConfigDialogState :
  IValueObserver<SharableDict<EndpointConfigBase>>,
  IDisposable {
  private readonly StateManager _stateManager;
  private readonly ConfigDialog _configDialog;
  private readonly EndpointDialogViewModel _cvm;
  private readonly EndpointId? _whitelistId;
  private readonly object _sync = new();
  private CancellationTokenSource _backgroundToken = new();
  private SharableDict<EndpointConfigBase> _endpointDict;
  private IDisposable? _disposer;

  public EndpointConfigDialogState(
    StateManager stateManager,
    ConfigDialog configDialog,
    EndpointDialogViewModel cvm,
    EndpointId? whitelistId) {
    _stateManager = stateManager;
    _configDialog = configDialog;
    _cvm = cvm;
    _whitelistId = whitelistId;
    _disposer = stateManager.SubscribeToEndpointDict(this);
  }

  public void Dispose() {
    Utility.ClearAndDispose(ref _disposer);
  }

  public void OnNext(SharableDict<EndpointConfigBase> dict) {
    lock (_sync) {
      _endpointDict = dict;
    }
  }

  public void OnSetCredentials() {
    if (!_cvm.TryMakeCredentials(out var newCreds, out var error)) {
      ShowMessageBox(error);
      return;
    }

    bool isKnown;
    lock (_sync) {
      // linear search but the dictionary is small
      isKnown = _endpointDict.Values.Any(ep => ep.Id.Equals(newCreds.Id));
    }

    if (isKnown && !newCreds.Id.Equals(_whitelistId)) {
      const string caption = "Modify existing connection?";
      var text = $"Are you sure you want to modify connection \"{newCreds.Id}\"";
      var dhm = new DeephavenMessageBox(caption, text, true);
      var dialogResult = dhm.ShowDialog(_configDialog);
      if (dialogResult != DialogResult.OK) {
        return;
      }
    }

    _stateManager.SetConfig(newCreds);
    if (_cvm.IsDefault) {
      _stateManager.SetDefaultEndpoint(newCreds.Id);
    }

    _configDialog.Close();
  }

  public void OnTestCredentials() {
    if (!_cvm.TryMakeCredentials(out var newCreds, out var error)) {
      ShowMessageBox(error);
      return;
    }

    _backgroundToken.Cancel();
    _backgroundToken = new CancellationTokenSource();

    _configDialog.SetTestResultsBox("Checking credentials");

    // Check credentials on its own thread
    var token = _backgroundToken.Token;
    Background.Run(() => TestCredentialsBackground(newCreds, token));
  }

  private void TestCredentialsBackground(PopulatedEndpointConfig config,
    CancellationToken token) {
    var state = "OK";
    try {
      // This operation might take some time.
      var temp = config.AcceptVisitor(
        core => (IDisposable)EndpointFactory.ConnectToCore(core),
        corePlus => EndpointFactory.ConnectToCorePlus(corePlus));
      Background.InvokeDispose(temp);
    } catch (Exception ex) {
      state = ex.Message;
    }

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _configDialog.SetTestResultsBox(state);
    }
  }

  private void ShowMessageBox(string error) {
    _configDialog.BeginInvoke(() => {
      var dhm = new DeephavenMessageBox("Please provide missing fields", error, false);
      dhm.ShowDialog(_configDialog);
    });
  }
}
