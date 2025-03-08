using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class CorePlusClientProvider :
  IValueObserver<StatusOr<RefCounted<SessionManager>>>,
  IValueObserver<PersistentQueryInfoMessage>,
  IValueObservable<StatusOr<RefCounted<DndClient>>>,
  IDisposable {
  private const string UnsetClientText = "[No Core+ Client]";
  private const string UnsetSessionManagerText = "[No Session Manager]";
  private const string UnsetPqInfoText = "[No PQ Info]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName _pqName;
  private readonly object _sync = new();
  private readonly FreshnessSource _freshness;
  private readonly Latch _subscribeDone = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _sessionManagerDisposer = null;
  private IDisposable? _pqInfoDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<DndClient>>> _observers = new();
  private StatusOr<RefCounted<SessionManager>> _sessionManager = UnsetSessionManagerText;
  private StatusOr<PersistentQueryInfoMessage> _pqInfo = UnsetPqInfoText;
  private StatusOr<RefCounted<DndClient>> _client = UnsetClientText;

  public CorePlusClientProvider(StateManager stateManager, EndpointId endpointId,
    PqName pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _freshness = new(_sync);
  }

  /// <summary>
  /// Subscribe to Enterprise Core+ client changes
  /// </summary>
  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<DndClient>>> observer) {
    lock (_sync) {
      SorUtil.AddObserverAndNotify(_observers, observer, _client, out _);
      if (_subscribeDone.TrySet()) {
        _sessionManagerDisposer = _stateManager.SubscribeToSessionManager(_endpointId, this);
        _pqInfoDisposer = _stateManager.SubscribeToPersistentQueryInfo(_endpointId, _pqName, this);
      }
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _sessionManagerDisposer);
      Utility.ClearAndDispose(ref _pqInfoDisposer);

      // Release our Deephaven resource asynchronously.
      SorUtil.Replace(ref _sessionManager, "[Disposed]");
      _pqInfo = "[Disposed]";
      SorUtil.Replace(ref _client, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<RefCounted<SessionManager>> sessionManager) {
    lock (_sync) {
      if (!_isDisposed.Value) {
        return;
      }
      SorUtil.Replace(ref _sessionManager, sessionManager);
      UpdateStateLocked();
    }
  }

  public void OnNext(PersistentQueryInfoMessage pqInfo) {
    lock (_sync) {
      if (!_isDisposed.Value) {
        return;
      }
      _pqInfo = pqInfo;
      UpdateStateLocked();
    }
  }

  private void UpdateStateLocked() {
    // Invalidate any background work that might be running.
    _freshness.Refresh();

    // Do we have a session and a PQInfo?
    if (!_sessionManager.GetValueOrStatus(out var sm, out var status) || 
        !_pqInfo.GetValueOrStatus(out var pq, out status)) {
      // No, transmit error status
      SorUtil.ReplaceAndNotify(ref _client, status, _observers);
      return;
    }

    // Is our PQInfo in the running state?
    if (!ControllerClient.IsRunning(pq.State.Status)) {
      SorUtil.ReplaceAndNotify(ref _client, $"PQ is in state {pq.State.Status}", _observers);
      return;
    }

    var smShared = sm.Share();
    Background.Run(() => {
      using var cleanup = smShared;
      UpdateStateInBackground(smShared, pq, _freshness.Current);
    });
  }

  private void UpdateStateInBackground(RefCounted<SessionManager> sm,
    PersistentQueryInfoMessage pq, FreshnessToken token) {
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
      if (token.IsCurrentUnsafe) {
        SorUtil.ReplaceAndNotify(ref _client, newState, _observers);
      }
    }
  }
}
