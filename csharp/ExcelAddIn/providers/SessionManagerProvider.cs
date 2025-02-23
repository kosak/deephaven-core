using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

/**
 * The job of this class is to observe EndpointConfigs for a given EndpointId,
 * and then provide SessionManagers. If the EndpointConfig does not refer to a Core
 * Plus instance, we notify an error. If it does, we try to connect to that instance in the
 * background. If that connection eventually succeeds, we notify our observers with the
 * corresponding SessionManager object. Otherwise we notify an error.
 */
internal class SessionManagerProvider :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObservable<StatusOr<SessionManager>> {
  private const string UnsetSessionManagerText = "[No SessionManager]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<SessionManager> _session = UnsetSessionManagerText;
  private readonly ObserverContainer<StatusOr<SessionManager>> _observers = new();

  public SessionManagerProvider(StateManager stateManager, EndpointId endpointId) {
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
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }

    // Now we have no more observers.
    IDisposable? disp;
    lock (_sync) {
      disp = Utility.Exchange(ref _upstreamSubscriptionDisposer, null);
    }
    disp?.Dispose();

    // Now we are not observing anyone. The object should be quiescent.

    // Release our Deephaven resource asynchronously.
    lock (_sync) {
      Background666.InvokeDispose(Utility.Exchange(ref _session, UnsetSessionManagerText));
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        _observers.SetStateAndNotify(ref _session, status);
        return;
      }

      _ = cbase.AcceptVisitor(
        _ => {
          // We are a CorePlus entity but we are getting credentials for core.
          _observers.SetStateAndNotify(ref _session,
            "Persistent Queries are not supported in Community Core");
          return Unit.Instance;
        },
        corePlus => {
          _observers.SetStateAndNotify(ref _session, "Trying to connect");
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
    using var cleanup = result;

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        _observers.SetStateAndNotify(ref _session, result);
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
