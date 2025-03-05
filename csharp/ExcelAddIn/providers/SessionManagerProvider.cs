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
  IObservable<StatusOr<SessionManager>>,
  IDisposable {
  private const string UnsetSessionManagerText = "[No SessionManager]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private bool _isDisposed = false;
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
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      _isDisposed = true;
      Utility.ClearAndDispose(ref _upstreamDisposer);
      ProviderUtil.SetState(ref _session, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }

      // Suppress background messages that might come in after this point.
      var cookie = _versionTracker.New();

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
          Background666.Run(() => OnNextBackground(corePlus, cookie));
          return Unit.Instance;
        });
    }
  }

  private void OnNextBackground(CorePlusEndpointConfig config,
    VersionTracker.Cookie versionCookie) {
    StatusOr<SessionManager> result;
    try {
      var sm = EndpointFactory.ConnectToCorePlus(config);
      result = StatusOr<SessionManager>.OfValue(sm);
    } catch (Exception ex) {
      result = StatusOr<SessionManager>.OfStatus(ex.Message);
    }
    using var cleanup = result;

    lock (_sync) {
      if (!_isDisposed && versionCookie.IsCurrent) {
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
