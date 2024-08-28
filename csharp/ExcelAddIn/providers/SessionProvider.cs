using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using System.Net;

namespace Deephaven.ExcelAddIn.Providers;

internal class SessionProvider : IObserver<StatusOr<CredentialsBase>>, IObservable<StatusOr<SessionBase>>, IDisposable {
  public static SessionProvider Create(EndpointId endpointId, CredentialsProviders credentialsProviders,
    WorkerThread workerThread) {
    var result = new SessionProvider(workerThread);
    result._credentialsDisposer = credentialsProviders.Subscribe(endpointId, result);
    return result;
  }

  private readonly WorkerThread _workerThread;
  private StatusOr<CredentialsBase> _credentials = StatusOr<CredentialsBase>.OfStatusUnknown();
  private StatusOr<SessionBase> _session = StatusOr<SessionBase>.OfStatusUnknown();
  private readonly ObserverContainer<StatusOr<SessionBase>> _observers = new();
  private IDisposable? _credentialsDisposer = null;

  private SessionProvider(WorkerThread workerThread) {
    _workerThread = workerThread;
  }

  public void Dispose() {
    if (_workerThread.InvokeIfRequired(Dispose)) {
      return;
    }

    // TODO(kosak)
    // I feel like we should send an OnComplete to any remaining observers

    Utility.Exchange(ref _credentialsDisposer, null)?.Dispose();
  }


  public IDisposable Subscribe(IObserver<StatusOr<SessionBase>> observer) {
    _workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observers.Add(observer, out _);
      observer.OnNext(_session);
    });

    return ActionAsDisposable.Create(() => {
      _workerThread.Invoke(() => {
        _observers.Remove(observer, out _);
      });
    });
  }

  public void OnNext(StatusOr<CredentialsBase> credentials) {
    if (_workerThread.InvokeIfRequired(() => OnNext(credentials))) {
      return;
    }

    _credentials = credentials;

    // Dispose existing session
    if (_session.GetValueOrStatus(out var sess, out _)) {
      sess.Dispose();
    }

    _session = StatusOr<SessionBase>.OfStatus("Trying to connect");
    if (!_credentials.GetValueOrStatus(out var creds, out var credStatus)) {
      _session = StatusOr<SessionBase>.OfStatus(credStatus);
    } else {
      _session = MakeSession(creds);
    }

    _observers.OnNext(_session);
  }

  public void Reconnect() {
    if (_workerThread.InvokeIfRequired(Reconnect)) {
      return;
    }

    // Can be accomplished by feeding ourselves our saved credentials (works both when
    // credentials are valid or invalid).
    OnNext(_credentials);
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  private StatusOr<SessionBase> MakeSession(CredentialsBase credentials) {
    try {
      var sb = SessionBaseFactory.Create(credentials, _workerThread);
      return StatusOr<SessionBase>.OfValue(sb);
    } catch (Exception ex) {
      return StatusOr<SessionBase>.OfStatus(ex.Message);
    }
  }
}
