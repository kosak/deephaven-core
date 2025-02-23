using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class CorePlusSessionProvider :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObservable<StatusOr<SessionManager>> {
  private const string UnsetSessionManagerText = "[Not connected]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<SessionManager> _session = UnsetSessionManagerText;
  private readonly ObserverContainer<StatusOr<SessionManager>> _observers = new();

  public CorePlusSessionProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to session changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<SessionManager>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _session);
      if (isFirst) {
        _upstreamSubscriptionDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<SessionManager>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      // You probably have a race here... You shouldn't return from this disposable
      // until you can promise that 'observer' will not be called. This is tricky because
      // you may have background items pending in your SequentialExecutor.
      if (!isLast) {
        return;
      }

      // Do these teardowns synchronously.
      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(_session.Move());
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        SetStateAndNotifyLocked(MakeState(status));
        return;
      }

      _ = cbase.AcceptVisitor(
        _ => {
          // We are a CorePlus entity but we are getting credentials for core.
          SetStateAndNotifyLocked(MakeState("Persistent Queries are not supported in Community Core"));
          return Unit.Instance;
        },
        corePlus => {
          SetStateAndNotifyLocked(MakeState("Trying to connect"));
          var cookie = _versionTracker.SetNewVersion();
          Background666.Run(() => OnNextBackground(corePlus, cookie));
          return Unit.Instance;
        });
    }
  }

  private void OnNextBackground(CorePlusEndpointConfig config,
    VersionTrackerCookie versionCookie) {
    StatusOr<SessionManager> result;
    try {
      // This operation might take some time.
      var sm = EndpointFactory.ConnectToCorePlus(config);
      result = StatusOr<SessionManager>.OfValue(sm);
    } catch (Exception ex) {
      result = StatusOr<SessionManager>.OfStatus(ex.Message);
    }
    using var newKeeper = KeepAlive.Register(result);

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        SetStateAndNotifyLocked(newKeeper.Move());
      }
    }
  }

  private static KeptAlive<StatusOr<SessionManager>> MakeState(string status) {
    var state = StatusOr<SessionManager>.OfStatus(status);
    return KeepAlive.Register(state);
  }

  private void SetStateAndNotifyLocked(KeptAlive<StatusOr<SessionManager>> newState) {
    Background666.InvokeDispose(_session);
    _session = newState;
    _observers.OnNext(newState.Target);
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
