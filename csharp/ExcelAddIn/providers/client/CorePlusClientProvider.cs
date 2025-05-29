using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class CorePlusClientProvider :
  IValueObserverWithCancel<StatusOr<RefCounted<SessionManager>>>,
  IValueObserverWithCancel<StatusOr<PersistentQueryInfoMessage>>,
  IValueObservable<StatusOr<RefCounted<DndClient>>> {
  private const string UnsetClientText = "[No Core+ Client]";
  private const string UnsetSessionManagerText = "[No Session Manager]";
  private const string UnsetPqInfoText = "[No PQ Info]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName _pqName;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IObservableCallbacks? _sessionManagerCallbacks = null;
  private IObservableCallbacks? _pqInfoCallbacks = null;
  private readonly ObserverContainer<StatusOr<RefCounted<DndClient>>> _observers = new();
  private StatusOr<RefCounted<SessionManager>> _sessionManager = UnsetSessionManagerText;
  private StatusOr<PersistentQueryInfoMessage> _pqInfo = UnsetPqInfoText;
  private StatusOr<RefCounted<DndClient>> _client = UnsetClientText;

  public CorePlusClientProvider(StateManager stateManager, EndpointId endpointId,
    PqName pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
  }

  /// <summary>
  /// Subscribe to Enterprise Core+ client changes
  /// </summary>
  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<RefCounted<DndClient>>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _client, out var isFirst);

      if (isFirst) {
        var voc1 = ValueObserverWithCancelWrapper.Create<StatusOr<RefCounted<SessionManager>>>(
          this, _upstreamTokenSource.Token);
        var voc2 = ValueObserverWithCancelWrapper.Create<StatusOr<PersistentQueryInfoMessage>>(
          this, _upstreamTokenSource.Token);

        _sessionManagerCallbacks = _stateManager.SubscribeToSessionManager(_endpointId, voc1);
        _pqInfoCallbacks = _stateManager.SubscribeToPersistentQueryInfo(_endpointId, _pqName, voc2);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    lock (_sync) {
      if (_sessionManager.GetValueOrStatus(out _, out _)) {
        // SessionManager parent is in error state, so propagate retry to it
        _sessionManagerCallbacks?.Retry();
      } else if (_pqInfo.GetValueOrStatus(out _, out _)) {
        // PQInfo Parent is in error state, so propagate retry to it (as a practical matter,
        // PqInfo will just ignore this Retry attempt, but that's not our problem)
        _pqInfoCallbacks?.Retry();
      } else {
        UpdateStateLocked();
      }
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<RefCounted<DndClient>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _sessionManagerCallbacks);
      Utility.ClearAndDispose(ref _pqInfoCallbacks);

      // Release our Deephaven resource asynchronously.
      StatusOrUtil.Replace(ref _sessionManager, UnsetSessionManagerText);
      StatusOrUtil.Replace(ref _pqInfo, UnsetPqInfoText);
      StatusOrUtil.Replace(ref _client, UnsetClientText);
    }
  }

  public void OnNext(StatusOr<RefCounted<SessionManager>> sessionManager,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.Replace(ref _sessionManager, sessionManager);
      UpdateStateLocked();
    }
  }

  public void OnNext(StatusOr<PersistentQueryInfoMessage> pqInfo,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.Replace(ref _pqInfo, pqInfo);
      UpdateStateLocked();
    }
  }

  private void UpdateStateLocked() {
    // Invalidate any background work that might be running.
    _backgroundTokenSource.Cancel();
    _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

    // Do we have a session and a PQInfo?
    if (!_sessionManager.GetValueOrStatus(out var sm, out var status) || 
        !_pqInfo.GetValueOrStatus(out var pq, out status)) {
      // No, transmit error status
      StatusOrUtil.ReplaceAndNotify(ref _client, status, _observers);
      return;
    }

    if (pq.State == null) {
      StatusOrUtil.ReplaceAndNotify(ref _client, "PQ is in unknown state", _observers);
      return;
    }

    // Is our PQInfo in the running state?
    if (!ControllerClient.IsRunning(pq.State.Status)) {
      StatusOrUtil.ReplaceAndNotify(ref _client, $"PQ is in state {pq.State.Status}", _observers);
      return;
    }

    // RefCounted item gets acquired on this thread.
    var smShared = sm.Share();
    var backgroundToken = _backgroundTokenSource.Token;
    Background.Run(() => {
      // RefCounted item gets released on this thread.
      using var cleanup = smShared;
      UpdateStateInBackground(smShared, pq, backgroundToken);
    });
  }

  private void UpdateStateInBackground(RefCounted<SessionManager> sm,
    PersistentQueryInfoMessage pq, CancellationToken token) {
    RefCounted<DndClient>? newRef = null;
    StatusOr<RefCounted<DndClient>> newState;
    try {
      var client = sm.Value.ConnectToPqById(pq.State.Serial, false);
      // Our value is the client, with a dependency on the SessionManager
      newRef = RefCounted.Acquire(client, sm);
      newState = newRef;
    } catch (Exception ex) {
      newState = ex.Message;
    }
    using var cleanup = newRef;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _client, newState, _observers);
    }
  }
}
