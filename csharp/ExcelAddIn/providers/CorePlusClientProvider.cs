using Deephaven.DheClient.Auth;
using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class CorePlusClientProvider :
  IObserver<StatusOr<SessionManager>>,
  IObserver<StatusOr<PersistentQueryInfoMessage>>,
  IObservable<StatusOr<DndClient>>, 
  IDisposable {
  private const string UnsetClientText = "[No Core+ Client]";
  private const string UnsetSessionManagerText = "[No Session Manager]";
  private const string UnsetPqInfoText = "[No PQ Info]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName _pqName;
  private readonly object _sync = new();
  private bool _subscribeDone = false;
  private bool _isDisposed = false;
  private IDisposable? _sessionManagerDisposer = null;
  private IDisposable? _pqInfoDisposer = null;
  private readonly ObserverContainer<StatusOr<DndClient>> _observers = new();
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<SessionManager> _sessionManager = UnsetSessionManagerText;
  private StatusOr<PersistentQueryInfoMessage> _pqInfo = UnsetPqInfoText;
  private StatusOr<DndClient> _client = UnsetClientText;

  public CorePlusClientProvider(StateManager stateManager, EndpointId endpointId,
    PqName pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
  }

  /// <summary>
  /// Subscribe to Enterprise Core+ client changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<DndClient>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _client, out _);
      if (!Utility.Exchange(ref _subscribeDone, true)) {
        _sessionManagerDisposer = _stateManager.SubscribeToSessionManager(_endpointId, this);
        _pqInfoDisposer = _stateManager.SubscribeToPersistentQueryInfo(_endpointId, _pqName, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<DndClient>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (Utility.Exchange(ref _isDisposed, true)) {
        return;
      }
      Utility.ClearAndDispose(ref _sessionManagerDisposer);
      Utility.ClearAndDispose(ref _pqInfoDisposer);

      // Release our Deephaven resource asynchronously.
      ProviderUtil.SetState(ref _sessionManager, "[Disposed]");
      ProviderUtil.SetState(ref _pqInfo, "[Disposed]");
      ProviderUtil.SetState(ref _client, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<SessionManager> sessionManager) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      ProviderUtil.SetState(ref _sessionManager, sessionManager);
      UpdateStateLocked();
    }
  }

  public void OnNext(StatusOr<PersistentQueryInfoMessage> pqInfo) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      ProviderUtil.SetState(ref _pqInfo, pqInfo);
      UpdateStateLocked();
    }
  }

  private void UpdateStateLocked() {
    // Invalidate any background work that might be running.
    var cookie = _versionTracker.New();

    // Do we have a session and a PQInfo?
    if (!_sessionManager.GetValueOrStatus(out var _, out var status) || 
        !_pqInfo.GetValueOrStatus(out var pq, out status)) {
      // No, transmit error status
      ProviderUtil.SetStateAndNotify(ref _client, status, _observers);
      return;
    }

    // Is our PQInfo in the running state?
    if (!ControllerClient.IsRunning(pq.State.Status)) {
      ProviderUtil.SetStateAndNotify(ref _client, $"PQ is in state {pq.State.Status}", _observers);
      return;
    }

    var smCopy = _sessionManager.Share();
    Background.Run(() => UpdateStateInBackground(smCopy, pq, cookie));
  }

  private void UpdateStateInBackground(StatusOr<SessionManager> smCopy,
    PersistentQueryInfoMessage pq, VersionTracker.Cookie cookie) {
    using var cleanup1 = smCopy;

    StatusOr<DndClient> newState;
    try {
      var (sm, _) = smCopy;
      var client = sm.ConnectToPqById(pq.State.Serial, false);
      // Our value is the client, with a dependency on the SessionManager
      newState = StatusOr<DndClient>.OfValue(client, smCopy);
    } catch (Exception ex) {
      newState = ex.Message;
    }
    using var cleanup2 = newState;

    lock (_sync) {
      if (cookie.IsCurrent) {
        ProviderUtil.SetStateAndNotify(ref _client, newState, _observers);
      }
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
