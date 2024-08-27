using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.ViewModels;
using System.ComponentModel;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Viewmodels;

public sealed class ConnectionManagerDialogRow : IObserver<StatusOr<CredentialsBase>>, IObserver<StatusOr<SessionBase>>,
  INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;

  private readonly StateManager _stateManager;

  private readonly object _sync = new();
  private StatusOr<CredentialsBase> _credentials = StatusOr<CredentialsBase>.OfStatusUnknown();
  private StatusOr<SessionBase> _session = StatusOr<SessionBase>.OfStatusUnknown();

  public ConnectionManagerDialogRow(string id, StateManager stateManager) {
    Id = id;
    _stateManager = stateManager;
  }

  public string Id { get; init; }

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

  public void SettingsClicked() {
    var creds = GetCredentialsSynced();
    // If we have valid credentials, 
    var cvm = creds.AcceptVisitor(
      crs => CredentialsDialogViewModel.OfIdAndCredentials(Id, crs),
      _ => CredentialsDialogViewModel.OfIdButOtherwiseEmpty(Id));
    var cd = CredentialsDialogFactory.Create(_stateManager, cvm);
    cd.Show();
  }

  public void ReconnectClicked() {
    _stateManager.Reconnect(new EndpointId(Id));
  }

  public void OnNext(StatusOr<CredentialsBase> value) {
    lock (_sync) {
      _credentials = value;
    }

    OnPropertyChanged(nameof(Status));
  }

  public void OnNext(StatusOr<SessionBase> value) {
    lock (_sync) {
      _session = value;
    }

    OnPropertyChanged(nameof(ServerType));
  }


  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  private StatusOr<CredentialsBase> GetCredentialsSynced() {
    lock (_sync) {
      return _credentials;
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
