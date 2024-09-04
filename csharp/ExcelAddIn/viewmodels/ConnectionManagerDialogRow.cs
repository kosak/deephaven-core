using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.ViewModels;
using System.ComponentModel;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using System.Net;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace Deephaven.ExcelAddIn.Viewmodels;

public sealed class ConnectionManagerDialogRowManager : IObserver<StatusOr<CredentialsBase>>,
  IObserver<StatusOr<SessionBase>>, IDisposable {

  public static ConnectionManagerDialogRowManager Create(EndpointId endpointId, StateManager stateManager) {
    var result = new ConnectionManagerDialogRowManager();
    var statusRow = new ConnectionManagerDialogRow(endpointId.Id, stateManager);
    // We watch for session and credential state changes in our ID
    var sessDisposable = stateManager.SubscribeToSession(endpointId, result);
    var credDisposable = stateManager.SubscribeToCredentials(endpointId, result);

    // And we also watch for credentials changes in the default session (just to keep
    // track of whether we are still the default)
    var dct = new DefaultCredentialsTracker(statusRow);
    var defaultCredDisposable = stateManager.SubscribeToDefaultCredentials(dct);

    // We'll do our AddRow on the GUI thread, and, while we're on the GUI thread, we'll add
    // our disposables to our saved disposables.
    cmDialog.Invoke(() => {
      _disposables.Add(sessDisposable);
      _disposables.Add(credDisposable);
      _disposables.Add(defaultCredDisposable);
      cmDialog.AddRow(statusRow);
    });

  }


  public void OnNext(StatusOr<CredentialsBase> value) {
    lock (_sync) {
      _credentials = value;
    }

    OnPropertyChanged(nameof(ServerType));
    OnPropertyChanged(nameof(IsDefault));
  }

  public void OnNext(StatusOr<SessionBase> value) {
    lock (_sync) {
      _session = value;
    }

    OnPropertyChanged(nameof(Status));
  }


  public void SettingsClicked() {
    var creds = GetCredentialsSynced();
    // If we have valid credentials, 
    var cvm = creds.AcceptVisitor(
      crs => CredentialsDialogViewModel.OfIdAndCredentials(Id, crs),
      _ => CredentialsDialogViewModel.OfIdButOtherwiseEmpty(Id));
    var cd = CredentialsDialogFactory.Create(stateManager, cvm);
    cd.Show();
  }

  public void DeleteClicked() {
    stateManager.SetCredentials();
  }

  public void ReconnectClicked() {
    stateManager.Reconnect(new EndpointId(Id));
  }

  public void IsDefaultClicked() {
    // If the box is already checked, do nothing.
    if (IsDefault) {
      return;
    }

    // If we don't have credentials, then we can't make them the default.
    if (!_credentials.GetValueOrStatus(out var creds, out _)) {
      return;
    }

    stateManager.SetDefaultCredentials(creds);
  }


  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }
}

internal class DefaultCredentialsTracker(ConnectionManagerDialogRow statusRow) : IObserver<StatusOr<CredentialsBase>> {
  public void OnNext(StatusOr<CredentialsBase> value) {
    statusRow.SetDefaultCredentials(value);
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }
}

public sealed class ConnectionManagerDialogRow(string id) : INotifyPropertyChanged {

  public event PropertyChangedEventHandler? PropertyChanged;

  private readonly object _sync = new();
  private StatusOr<CredentialsBase> _credentials = StatusOr<CredentialsBase>.OfStatus("[Not set]");
  private StatusOr<SessionBase> _session = StatusOr<SessionBase>.OfStatus("[Not connected]");
  private StatusOr<CredentialsBase> _defaultCredentials = StatusOr<CredentialsBase>.OfStatus("[Not set]");

  public string Id { get; init; } = id;

  public string Status {
    get {
      var session = GetSessionSynced();
      // If we have a valid session, return "[Connected]", otherwise pass through the status text we have.
      return session.AcceptVisitor(
        _ => "[Connected]",
        status => status);
    }
  }

  public string ServerType {
    get {
      var creds = GetCredentialsSynced();
      // Nested AcceptVisitor!!
      // If we have valid credentials, determine whether they are for Core or Core+ and return the appropriate string.
      // Otherwise (if we have invalid credentials), ignore their status text and just say "[Unknown]".
      return creds.AcceptVisitor(
        crs => crs.AcceptVisitor(_ => "Core", _ => "Core+"),
        _ => "[Unknown]");

    }
  }

  public bool IsDefault {
    get {
      var creds = GetCredentialsSynced();
      var defaultCreds = GetDefaultCredentialsSynced();
      return creds.GetValueOrStatus(out var creds1, out _) &&
             defaultCreds.GetValueOrStatus(out var creds2, out _) &&
             creds1.Id == creds2.Id;
    }
  }

  public void SetDefaultCredentials(StatusOr<CredentialsBase> creds) {
    lock (_sync) {
      _defaultCredentials = creds;
    }
    OnPropertyChanged(nameof(IsDefault));
  }

  private StatusOr<CredentialsBase> GetCredentialsSynced() {
    lock (_sync) {
      return _credentials;
    }
  }

  private StatusOr<CredentialsBase> GetDefaultCredentialsSynced() {
    lock (_sync) {
      return _defaultCredentials;
    }
  }

  private StatusOr<SessionBase> GetSessionSynced() {
    lock (_sync) {
      return _session;
    }
  }

  private void OnPropertyChanged(string name) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}
