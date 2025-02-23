using Deephaven.DheClient.Auth;
using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class CorePlusClientProvider :
  IObserver<StatusOr<SessionManager>>,
  IObserver<PersistentQueryInfoMessage>,
  IObservable<StatusOr<DndClient>> {
  private const string UnsetClientText = "[Not Connected to Core+ Client]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly string _pqName;
  private readonly object _sync = new();
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<SessionManager> _sessionManager = UnsetSessionManagerText;
  private StatusOr<PersistentQueryInfoMessage> _pqInfo = UnsetPqInfoText;
  private StatusOr<DndClient> _client = UnsetClientText;
  private readonly ObserverContainer<StatusOr<DndClient>> _observers = new();

  public CorePlusClientProvider(StateManager stateManager, EndpointId endpointId,
    string pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
  }

  /// <summary>
  /// Subscribe to Enterprise Core+ client changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<DndClient>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _client);
      if (isFirst) {
        _sessionManagerDisposer = _stateManager.SubscribeToSessionManager(_endpointId, this);
        _pqInfoDisposer = _stateManager.SubscribeToPqInfo(_endpointId, _pqName, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<DndClient>> observer) {
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }
    lock (_sync) {
      // Do these teardowns synchronously.
      Utility.Exchange(ref _sessionManagerDisposer, null)?.Dispose();
      Utility.Exchange(ref _pqInfoDisposer, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(Utility.Exchange(ref _sessionManager, UnsetSessionManagerText));
      Background666.InvokeDispose(Utility.Exchange(ref _client, UnsetClientText));
    }
  }

  public void OnNext(StatusOr<SessionManager> sessionManager) {
    lock (_sync) {
      SetState(ref _sessionManager, status);
      UpdateStateLocked();
    }
  }

  public void OnNext(StatusOr<PersistentQueryInfoMessage> message) {
    lock (_sync) {
      SetState(ref _pqInfo, message);
      UpdateStateLocked();
    }
  }

  private void UpdateStateLocked() {
    // Do we have a session?
    if (!_sessionManager.GetValueOrStatus(out var sm, out var status)) {
      _observers.SetStateAndNotify(ref _client, status);
      return;
    }

    // Do we have a PQInfo?
    if (!_pqInfo.GetValueOrStatus(out var pq, out status)) {
      _observers.SetStateAndNotify(ref _client, status);
      return;
    }

    // Is our PQInfo in the running state?
    if (!ControllerClient.IsRunning(pq.State.Status)) {
      _observers.SetStateAndNotify(ref _client, $"PQ is in state {pq.State.Status}");
      return;
    }

    var smCopy = _sessionManager.Copy();
    var cookie = _versionTracker.SetNewVersion();
    Background666.Run(() => UpdateStateInBackground(smCopy, pq, cookie));
  }

  private void UpdateStateInBackground(StatusOr<SessionManager> smCopy,
    PersistentQueryInfoMessage pq, VersionTrackerCookie cookie) {
    using var cleanup1 = smCopy;

    StatusOr<DndClient> newState;
    try {
      var (sm, _) = smCopy;
      var client = sm.ConnectToPqById(pq.State.Serial, false);
      // Keep a dependency on sm
      newState = StatusOr<DndClient>.OfValue(client, smCopy);
    } catch (Exception ex) {
      newState = ex.Message;
    }
    using var cleanup = newState;

    lock (_sync) {
      if (cookie.IsCurrent) {
        _observers.SetStateAndNotify(ref _client, newState);
      }
    }
  }
}
