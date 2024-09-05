using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SessionProvider : IObserver<StatusOr<CredentialsBase>>, IObservable<StatusOr<SessionBase>>  {
  public static SessionProvider Create(EndpointId endpointId, StateManager sm, Action onDispose) {

    var result = new SessionProvider(endpointId, sm.WorkerThread, onDispose);
    var usd = sm.SubscribeToCredentials(endpointId, result);
    result._upstreamSubscriptionDisposer = usd;
    return result;
  }

  private readonly EndpointId _endpointId;
  private readonly WorkerThread _workerThread;
  private Action? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private StatusOr<SessionBase> _session = StatusOr<SessionBase>.OfStatus("[Not connected]");
  private readonly ObserverContainer<StatusOr<SessionBase>> _observers = new();
  private readonly VersionTracker _versionTracker = new();

  public SessionProvider(EndpointId endpointId, WorkerThread workerThread, Action onDispose) {
    _endpointId = endpointId;
    _workerThread = workerThread;
    _onDispose = onDispose;
  }

  /// <summary>
  /// Subscribe to session changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<SessionBase>> observer) {
    _workerThread.Invoke(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_session);
    });

    return _workerThread.InvokeWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeSessionState();
    });
  }

  public void OnNext(StatusOr<CredentialsBase> credentials) {
    if (_workerThread.InvokeIfRequired(() => OnNext(credentials))) {
      return;
    }

    DisposeSessionState();

    if (!credentials.GetValueOrStatus(out var cb, out var status)) {
      _observers.SetAndSendStatus(ref _session, status);
      return;
    }

    _observers.SetAndSendStatus(ref _session, "Trying to connect");

    var cookie = _versionTracker.SetNewVersion();
    Utility.RunInBackground(() => CreateSessionBaseInSeparateThread(cb, cookie));
  }

  private void CreateSessionBaseInSeparateThread(CredentialsBase credentials, VersionTrackerCookie versionCookie) {
    StatusOr<SessionBase> result;
    try {
      // This operation might take some time.
      var sb = SessionBaseFactory.Create(credentials, _workerThread);
      result = StatusOr<SessionBase>.OfValue(sb);
    } catch (Exception ex) {
      result = StatusOr<SessionBase>.OfStatus(ex.Message);
    }

    // Some time has passed. It's possible that the VersionTracker has been reset
    // with a newer version. If so, we should throw away our work and leave.
    if (!versionCookie.IsCurrent) {
      // Our results are moot. Dispose of them.
      if (result.GetValueOrStatus(out var sb, out _)) {
        sb.Dispose();
      }
      return;
    }

    // Our results are valid. Keep them and tell everyone about it (on the worker thread).
    _workerThread.Invoke(() => _observers.SetAndSend(ref _session, result));
  }

  private void DisposeSessionState() {
    if (_workerThread.InvokeIfRequired(DisposeSessionState)) {
      return;
    }

    _ = _session.GetValueOrStatus(out var oldSession, out _);
    _observers.SetAndSendStatus(ref _session, "Disposing Session");

    if (oldSession != null) {
      Utility.RunInBackground(oldSession.Dispose);
    }
  }

  // public void Reconnect() {
  //   // Get on the worker thread if not there already.
  //   if (workerThread.InvokeIfRequired(Reconnect)) {
  //     return;
  //   }
  //
  //   // We implement this as a SetCredentials call, with credentials we already have.
  //   if (_credentials.GetValueOrStatus(out var creds, out _)) {
  //     SetCredentials(creds);
  //   }
  // }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }
}
