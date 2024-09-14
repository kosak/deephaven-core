using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class CorePlusSessionProvider :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObservable<StatusOr<SessionManager>> {
  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly EndpointId _endpointId;
  private Action? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private StatusOr<SessionManager> _session = StatusOr<SessionManager>.OfStatus("[Not connected]");
  private readonly ObserverContainer<StatusOr<SessionManager>> _observers = new();
  private readonly VersionTracker _versionTracker = new();

  public CorePlusSessionProvider(StateManager stateManager, EndpointId endpointId, Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _endpointId = endpointId;
    _onDispose = onDispose;
  }

  public void Init() {
    _upstreamSubscriptionDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
  }

  /// <summary>
  /// Subscribe to session changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<SessionManager>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_session);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeSessionState();
    });
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    if (_workerThread.EnqueueOrNop(() => OnNext(credentials))) {
      return;
    }

    DisposeSessionState();

    if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
      _observers.SetAndSendStatus(ref _session, status);
      return;
    }

    _ = cbase.AcceptVisitor(
      _ => {
        // We are a CorePlus entity but we are getting credentials for core.
        _observers.SetAndSendStatus(ref _session, "Persistent Queries are not supported in Community Core");
        return Unit.Instance;
      },
      corePlus => {
        _observers.SetAndSendStatus(ref _session, "Trying to connect");

        var cookie = _versionTracker.SetNewVersion();
        Utility.RunInBackground(() => CreateSessionManagerInSeparateThread(corePlus, cookie));
        return Unit.Instance;
      });
  }

  private void CreateSessionManagerInSeparateThread(CorePlusEndpointConfig config,
    VersionTrackerCookie versionCookie) {
    SessionManager? sm = null;
    StatusOr<SessionManager> result;
    try {
      // This operation might take some time.
      sm = EndpointFactory.ConnectToCorePlus(config, _workerThread);
      result = StatusOr<SessionManager>.OfValue(sm);
    } catch (Exception ex) {
      result = StatusOr<SessionManager>.OfStatus(ex.Message);
    }

    // Some time has passed. It's possible that the VersionTracker has been reset
    // with a newer version. If so, we should throw away our work and leave.
    if (!versionCookie.IsCurrent) {
      sm?.Dispose();
      return;
    }

    // Our results are valid. Keep them and tell everyone about it (on the worker thread).
    _workerThread.EnqueueOrRun(() => _observers.SetAndSend(ref _session, result));
  }

  private void DisposeSessionState() {
    if (_workerThread.EnqueueOrNop(DisposeSessionState)) {
      return;
    }

    _ = _session.GetValueOrStatus(out var oldSession, out _);
    _observers.SetAndSendStatus(ref _session, "Disposing Session");

    if (oldSession != null) {
      Utility.RunInBackground(oldSession.Dispose);
    }
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
