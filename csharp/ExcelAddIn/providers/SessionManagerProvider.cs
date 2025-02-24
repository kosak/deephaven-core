using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

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
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<SessionManager>> _observers = new();
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<SessionManager> _session = UnsetSessionManagerText;

  public SessionManagerProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to session changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<SessionManager>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _session, out var isFirst);
      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<SessionManager>> observer) {
    // Do not do this under lock, because we don't want to wait while holding a lock.
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }

    // At this point we have no observers.
    IDisposable? disp;
    lock (_sync) {
      disp = Utility.Exchange(ref _upstreamDisposer, null);
    }
    disp?.Dispose();

    // At this point we are not observing anything.
    // Release our Deephaven resource asynchronously.
    lock (_sync) {
      Background666.InvokeDispose(Utility.Exchange(ref _session, UnsetSessionManagerText));
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _session, status, _observers);
        return;
      }

      _ = cbase.AcceptVisitor(
        _ => {
          // We are a CorePlus entity but we are getting credentials for core.
          ProviderUtil.SetStateAndNotify(ref _session,
            "Persistent Queries are not supported in Community Core", _observers);
          return Unit.Instance;
        },
        corePlus => {
          ProviderUtil.SetStateAndNotify(ref _session, "Trying to connect", _observers);
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
      var sm = EndpointFactory.ConnectToCorePlus(config);
      result = StatusOr<SessionManager>.OfValue(sm);
    } catch (Exception ex) {
      result = StatusOr<SessionManager>.OfStatus(ex.Message);
    }
    using var cleanup = result;

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        ProviderUtil.SetStateAndNotify(ref _session, result, _observers);
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
