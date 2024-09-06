using System.ComponentModel;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Viewmodels;

public sealed class ConnectionManagerDialogRow(string id) : INotifyPropertyChanged {

  public event PropertyChangedEventHandler? PropertyChanged;

  private readonly object _sync = new();
  private StatusOr<CredentialsBase> _credentials = StatusOr<CredentialsBase>.OfStatus("[Not set]");
  private StatusOr<SessionBase> _session = StatusOr<SessionBase>.OfStatus("[Not connected]");
  private EndpointId? _defaultEndpointId = null;

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
      var credentials = GetCredentialsSynced();
      var defaultEp = GetDefaultEndpointIdSynced();
      return credentials.GetValueOrStatus(out var creds, out _) &&
             creds.Id.Equals(defaultEp);
    }
  }

  public StatusOr<CredentialsBase> GetCredentialsSynced() {
    lock (_sync) {
      return _credentials;
    }
  }

  public void SetCredentialsSynced(StatusOr<CredentialsBase> value) {
    lock (_sync) {
      _credentials = value;
    }

    OnPropertyChanged(nameof(ServerType));
    OnPropertyChanged(nameof(IsDefault));
  }

  public EndpointId? GetDefaultEndpointIdSynced() {
    lock (_sync) {
      return _defaultEndpointId;
    }
  }

  public void SetDefaultEndpointIdSynced(EndpointId? value) {
    lock (_sync) {
      _defaultEndpointId = value;
    }
    OnPropertyChanged(nameof(IsDefault));
  }

  public StatusOr<SessionBase> GetSessionSynced() {
    lock (_sync) {
      return _session;
    }
  }

  public void SetSessionSynced(StatusOr<SessionBase> value) {
    lock (_sync) {
      _session = value;
    }
    OnPropertyChanged(nameof(Status));
  }

  private void OnPropertyChanged(string name) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}
