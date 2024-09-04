using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.ViewModels;
using System.ComponentModel;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Net;

namespace Deephaven.ExcelAddIn.Viewmodels;

public sealed class ConnectionManagerDialogRowManager(WorkerThread workerThread) : IObserver<StatusOr<CredentialsBase>>,
  IObserver<StatusOr<SessionBase>>, IDisposable {

  public static ConnectionManagerDialogRowManager Create(ConnectionManagerDialogRow row,
    EndpointId endpointId, StateManager stateManager) {
    var result = new ConnectionManagerDialogRowManager();
    result.Resubscribe();
    return result;
  }

  private List<IDisposable> _disposables;

  private void Resubscribe() {
    if (workerThread.InvokeIfRequired(Resubscribe)) {
      return;
    }

    if (_disposables.Count != 0) {
      throw new Exception("State error: already subscribed");
    }
    // We watch for session and credential state changes in our ID
    _disposables.Add(stateManager.SubscribeToSession(endpointId, result));
    _disposables.Add(SubscribeToCredentials(endpointId, result));
    // Now we have a problem. We would also like to watch for credential
    // state changes in the default session. But the default session
    // has the same observable type (IObservable<StatusOr<SessionBase>>)
    // as the specific session we are watching. To work around this,
    // we create an Observer that translates StatusOr<SessionBase> to
    // MyWrappedSOSB and then we subscribe to that.
    var wrappedResult = Utility.SuperNubbin<StatusOr<CredentialsBase>, MyWrappedSOCB>(this);
    _disposables.Add(stateManager.SubscribeToDefaultCredentials(wrappedResult));
  }

  private void Unsubcribe() {
    if (workerThread.InvokeIfRequired(Unsubcribe)) {
      return;
    }
    var temp = _disposables.ToArray();
    _disposables.Clear();

    foreach (var disposable in temp) {
      disposable.Dispose();
    }
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

  public void OnNext(DefaultStatusOr value) {
    statusRow.SetDefaultCredentials(value);
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
    // Strategy:
    // 1. Unsubscribe to everything
    // 2. If it turns out that we were the last subscriber to the session, then great, the
    //    delete can proceed.
    // 3. Otherwise (there is some other subscriber to the session), then the delete operation
    //    is denied. In that case we resubscribe.
    Unsubcribe();
    stateManager.LooseyGoosey(() => Resubscribe());
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
