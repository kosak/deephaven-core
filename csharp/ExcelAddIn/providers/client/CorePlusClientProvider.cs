using Deephaven.Dhe_NetClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Refcounting;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class CorePlusClientProvider :
  IValueObserverWithCancel<StatusOr<SessionManager>>,
  IValueObserverWithCancel<StatusOr<PersistentQueryInfoMessage>>,
  IValueObservable<StatusOr<DndClient>> {
  private const string UnsetClientText = "No Core+ Client";
  private const string UnsetSessionManagerText = "No Session Manager";
  private const string UnsetPqInfoText = "No PQ Info";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName _pqName;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IObservableCallbacks? _sessionManagerCallbacks = null;
  private IObservableCallbacks? _pqInfoCallbacks = null;
  private readonly ObserverContainer<StatusOr<DndClient>> _observers = new();
  private readonly StatusOrHolder<SessionManager> _sessionManager = new(UnsetSessionManagerText);
  private readonly StatusOrHolder<PersistentQueryInfoMessage> _pqInfo = new(UnsetPqInfoText);
  private readonly StatusOrHolder<DndClient> _client = new(UnsetClientText);

  public CorePlusClientProvider(StateManager stateManager, EndpointId endpointId,
    PqName pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
  }

  /// <summary>
  /// Subscribe to Enterprise Core+ client changes
  /// </summary>
  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<DndClient>> observer) {
    lock (_sync) {
      _client.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        var voc1 = ValueObserverWithCancelWrapper.Create<StatusOr<SessionManager>>(
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
      if (!_sessionManager.GetValueOrStatus(out _, out _)) {
        // SessionManager parent is in error state, so propagate retry to it
        _sessionManagerCallbacks?.Retry();
      } else if (!_pqInfo.GetValueOrStatus(out _, out _)) {
        // PQInfo Parent is in error state, so propagate retry to it (as a practical matter,
        // PqInfo will just ignore this Retry attempt, but that's not our problem)
        _pqInfoCallbacks?.Retry();
      } else {
        UpdateStateLocked();
      }
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<DndClient>> observer) {
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
      _sessionManager.Replace(UnsetSessionManagerText);
      _pqInfo.Replace(UnsetPqInfoText);
      _client.Replace(UnsetClientText);
    }
  }

  public void OnNext(StatusOr<SessionManager> sessionManager,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _sessionManager.Replace(sessionManager);
      UpdateStateLocked();
    }
  }

  public void OnNext(StatusOr<PersistentQueryInfoMessage> pqInfo,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _pqInfo.Replace(pqInfo);
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
      _client.ReplaceAndNotify(status, _observers);
      return;
    }

    if (pq.State == null) {
      _client.ReplaceAndNotify("PQ is in unknown state", _observers);
      return;
    }

    // Is our PQInfo in the running state?
    var pqStatus = pq.State.Status;
    if (!ControllerClient.IsRunning(pqStatus)) {
      // No it's not, so notify the state we are in, refined by whether that is a transient
      // or final state
      var statusText = pqStatus.ToString();
      StatusOr<DndClient> result;
      if (ControllerClient.IsTerminal(pqStatus)) {
        result = StatusOr<DndClient>.OfFixed(statusText);
      } else {
        result = StatusOr<DndClient>.OfTransient(statusText);
      }
      _client.ReplaceAndNotify(result, _observers);
      return;
    }

    // RefCounted item gets acquired on this thread.
    var sharedDisposer = Repository.Share(sm);
    var backgroundToken = _backgroundTokenSource.Token;
    Background.Run(() => {
      // RefCounted item gets released on this thread.
      using var cleanup = sharedDisposer;
      UpdateStateInBackground(sm, pq, backgroundToken);
    });
  }

  private void UpdateStateInBackground(SessionManager sm,
    PersistentQueryInfoMessage pq, CancellationToken token) {
    IDisposable? sharedDisposer = null;
    StatusOr<DndClient> result;
    try {
      var client = sm.ConnectToPqById(pq.State.Serial, false);
      // Our value is the client, with a dependency on the SessionManager
      sharedDisposer = Repository.Register(client, sm);
      result = client;
    } catch (Exception ex) {
      result = ex.Message;
    }
    using var cleanup = sharedDisposer;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _client.ReplaceAndNotify(result, _observers);
    }
  }
}
