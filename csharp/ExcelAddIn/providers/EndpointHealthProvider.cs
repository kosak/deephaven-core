using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Auth;

namespace Deephaven.ExcelAddIn.Providers;

public interface IObserverWithCookie<T> {
  void OnNextWithCookie(T value, VersionTracker.Cookie cookie);
  void OnCompletedWithCookie(VersionTracker.Cookie cookie);
  void OnErrorWithCookie(Exception ex, VersionTracker.Cookie cookie);
}

public class ObserverWithCookie<T>(
  IObserverWithCookie<T> owner,
  VersionTracker.Cookie cookie)
  : IObserver<T> {
  public void OnNext(T value) {
    owner.OnNextWithCookie(value, cookie);
  }

  public void OnCompleted() {
    owner.OnCompletedWithCookie(cookie);
  }

  public void OnError(Exception ex) {
    owner.OnErrorWithCookie(ex, cookie);
  }
}

/**
 * The job of this class is to observe EndpointConfig notifications for a given EndpointId,
 * and then provide EndpointHealth notifications. These notifications are either a placeholder
 * EndpointHealth object (if healthy) or a status message (if not).
 *
 * The logic works as follows. Subscribe to EndpointConfigs for a given EndpointId.
 * When a valid EndpoingConfig arrives, subscribe to 
 * *
 * Observes the EndpointConfig. When a valid EndpointConfig is received, observes
 * the appropriate CoreClientProvider or CorePlusSessionProvider.
 * When those things provide responses, translate them to ConnectionHealth messages.
 * We use StatusOr&lt;ConnectionHealth&gt; . A healthy connection sends a StatusOr
 * with value set to ConnectionHealth (this is an object without any members). On the
 * other hand, an unhealthy connection sends a StatusOr with the status text sent to
 * whatever status text was received from upstream.
 */
internal class EndpointHealthProvider :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObserverWithCookie<StatusOr<Client>>,
  IObserverWithCookie<StatusOr<SessionManager>>,
  IObservable<StatusOr<EndpointHealth>>,
  IDisposable {
  private const string UnsetHealthString = "[No Config]";
  private const string ConnectionOkString = "OK";

  private readonly StateManager _stateManager;
  private readonly string _endpointId;
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private IDisposable? _upstreamConfigDisposer = null;
  private IDisposable? _upstreamClientOrSessionDisposer = null;
  private StatusOr<EndpointHealth> _endpointHealth = UnsetHealthString;
  private readonly ObserverContainer<StatusOr<EndpointHealth>> _observers = new();
  private readonly VersionTracker _versionTracker = new();

  public EndpointHealthProvider(StateManager stateManager, string endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to connection health changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<EndpointHealth>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _endpointHealth, out var isFirst);
      if (isFirst) {
        _upstreamConfigDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<EndpointHealth>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      _isDisposed = true;
      Utility.ClearAndDispose(ref _upstreamConfigDisposer);
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);
      ProviderUtil.SetState(ref _endpointHealth, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }

      // We use this cookie approach because we are switching subscriptions
      // midstream and we don't want to get confused by stale notifications
      // from something we just unsubscribed from.
      var cookie = _versionTracker.New();
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);

      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _endpointHealth, status, _observers);
        return;
      }

      ProviderUtil.SetStateAndNotify(ref _endpointHealth, "[Unknown]", _observers);

      // Upstream has core or corePlus value. Use the visitor to figure 
      // out which one and subscribe to it.
      _upstreamClientOrSessionDisposer = cbase.AcceptVisitor(
        (CoreEndpointConfig _) => {
          var observerWithCookie = new ObserverWithCookie<StatusOr<Client>>(this, cookie);
          return _stateManager.SubscribeToCoreClient(_endpointId, observerWithCookie);
        },
        (CorePlusEndpointConfig _) => {
          var observerWithCookie = new ObserverWithCookie<StatusOr<SessionManager>>(this, cookie);
          return _stateManager.SubscribeToCorePlusSession(_endpointId, observerWithCookie);
        });
    }
  }

  public void OnCompleted() {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      // TODO(kosak)
      throw new NotImplementedException();
    }
  }

  public void OnError(Exception error) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      // TODO(kosak)
      throw new NotImplementedException();
    }
  }

  public void OnNextWithCookie(StatusOr<Client> client, VersionTracker.Cookie cookie) {
    lock (_sync) {
      if (_isDisposed || !cookie.IsCurrent) {
        return;
      }
      // If valid value, then we notify with the ConnectionOKString. Otherwise we pass through
      // the status.
      var message = client.AcceptVisitor(_ => ConnectionOkString, s => s);
      ProviderUtil.SetStateAndNotify(ref _endpointHealth, message, _observers);
    }
  }

  public void OnNextWithCookie(StatusOr<SessionManager> sm, VersionTracker.Cookie cookie) {
    lock (_sync) {
      if (_isDisposed || !cookie.IsCurrent) {
        return;
      }
      // If valid value, then we notify with the ConnectionOKString. Otherwise we pass through
      // the status.
      var message = sm.AcceptVisitor(_ => ConnectionOkString, s => s);
      ProviderUtil.SetStateAndNotify(ref _endpointHealth, message, _observers);
    }
  }

  public void OnCompletedWithCookie(VersionTracker.Cookie cookie) {
    lock (_sync) {
      if (_isDisposed || !cookie.IsCurrent) {
        return;
      }
      // TODO(kosak)
      throw new NotImplementedException();
    }
  }

  public void OnErrorWithCookie(Exception ex, VersionTracker.Cookie cookie) {
    lock (_sync) {
      if (_isDisposed || !cookie.IsCurrent) {
        return;
      }
      // TODO(kosak)
      throw new NotImplementedException();
    }
  }
}
