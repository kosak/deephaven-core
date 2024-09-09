using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/// <summary>
/// The job of this class is to observe EndpointConfig notifications for a given EndpointId,
/// and then provide EndpointHealth notifications. These notifications are either a placeholder
/// EndpointHealth object (if healthy) or a status message (if not).
///
/// The logic works as follows. Subscribe to EndpointConfigs for a given EndpointId.
/// When a valid EndpoingConfig arrives, subscribe to 
///
/// Observes the EndpointConfig. When a valid EndpointConfig is received, observes
/// the appropriate CoreClientProvider or CorePlusSessionProvider.
/// When those things provide responses, translate them to ConnectionHealth messages.
/// We use StatusOr&lt; ConnectionHealth & gt; . A healthy connection sends a StatusOr
/// with value set to ConnectionHealth (this is an object without any members). On the
/// other hand, an unhealthy connection sends a StatusOr with the status text sent to
/// whatever status text was received from upstream.
/// </summary>
internal class EndpointHealthProvider :
  IValueObserver<StatusOr<EndpointConfigBase>>,
  IValueObserver<StatusOr<RefCounted<Client>>>,
  IValueObserver<StatusOr<RefCounted<SessionManager>>>,
  IValueObservable<StatusOr<EndpointHealth>>,
  IDisposable {
  private const string UnsetHealthString = "[No Config]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly FreshnessTokenSource _freshness;
  private readonly Latch _isSubscribed = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamConfigDisposer = null;
  private IDisposable? _upstreamClientOrSessionDisposer = null;
  private readonly ObserverContainer<StatusOr<EndpointHealth>> _observers = new();
  private StatusOr<EndpointHealth> _endpointHealth = UnsetHealthString;

  public EndpointHealthProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _freshness = new(_sync);
  }

  /// <summary>
  /// Subscribe to connection health changes
  /// </summary>
  public IDisposable Subscribe(IValueObserver<StatusOr<EndpointHealth>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _endpointHealth, out _);
      if (_isSubscribed.TrySet()) {
        _upstreamConfigDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
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

      Utility.ClearAndDispose(ref _upstreamConfigDisposer);
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);
      StatusOrUtil.Replace(ref _endpointHealth, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      var token = _freshness.Refresh();
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);

      if (!credentials.GetValueOrStatus(out var creds, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _endpointHealth, status, _observers);
        return;
      }

      StatusOrUtil.ReplaceAndNotify(ref _endpointHealth, "[Unknown]", _observers);

      // Upstream has core or corePlus value. Use the visitor to figure 
      // out which one and subscribe to it.
      _upstreamClientOrSessionDisposer = creds.AcceptVisitor<IDisposable?>(
        (EmptyEndpointConfig _) => {
          StatusOrUtil.ReplaceAndNotify(ref _endpointHealth, UnsetHealthString, _observers);
          return null;
        },
        (CoreEndpointConfig _) => {
          var fobs = new ValueObserverFreshnessFilter<StatusOr<RefCounted<Client>>>(
            this, token);
          return _stateManager.SubscribeToCoreClient(_endpointId, fobs);
        },
        (CorePlusEndpointConfig _) => {
          var fobs = new ValueObserverFreshnessFilter<StatusOr<RefCounted<SessionManager>>>(
            this, token);
          return _stateManager.SubscribeToSessionManager(_endpointId, fobs);
        });
    }
  }

  public void OnNext(StatusOr<RefCounted<Client>> client) {
    OnNextHelper(client);
  }

  public void OnNext(StatusOr<RefCounted<SessionManager>> sessionManager) {
    OnNextHelper(sessionManager);
  }

  private void OnNextHelper<T>(StatusOr<T> statusOr) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      StatusOr<EndpointHealth> result;
      if (statusOr.GetValueOrStatus(out _, out var status)) {
        result = new EndpointHealth();
      } else {
        result = status;
      }
      StatusOrUtil.ReplaceAndNotify(ref _endpointHealth, result, _observers);
    }
  }
}
