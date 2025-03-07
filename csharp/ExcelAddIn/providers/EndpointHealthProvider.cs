using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

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
  IStatusObserver<EndpointConfigBase>,
  IStatusObserver<RefCounted<Client>>,
  IStatusObserver<RefCounted<SessionManager>>,
  IStatusObservable<EndpointHealth>,
  IDisposable {
  private const string UnsetHealthString = "[No Config]";
  private const string ConnectionOkString = "OK";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly FreshnessSource _freshness;
  private readonly Latch _isSubscribed = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamConfigDisposer = null;
  private IDisposable? _upstreamClientOrSessionDisposer = null;
  private StatusOr<EndpointHealth> _endpointHealth = UnsetHealthString;
  private readonly ObserverContainer<EndpointHealth> _observers = new();

  public EndpointHealthProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _freshness = new(_sync);
  }

  /// <summary>
  /// Subscribe to connection health changes
  /// </summary>
  public IDisposable Subscribe(IStatusObserver<EndpointHealth> observer) {
    lock (_sync) {
      SorUtil.AddObserverAndNotify(_observers, observer, _endpointHealth, out _);
      if (_isSubscribed.TrySet()) {
        _upstreamConfigDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IStatusObserver<EndpointHealth> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }

      Utility.ClearAndDispose(ref _upstreamConfigDisposer);
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);
      SorUtil.Replace(ref _endpointHealth, "[Disposed]");
    }
  }

  void IStatusObserver<EndpointConfigBase>.OnStatus(string status) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      _freshness.Reset();
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);
      SorUtil.ReplaceAndNotify(ref _endpointHealth, status, _observers);
    }
  }

  public void OnNext(EndpointConfigBase credentials) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      _freshness.Reset();
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);
      SorUtil.ReplaceAndNotify(ref _endpointHealth, "[Unknown]", _observers);

      // Upstream has core or corePlus value. Use the visitor to figure 
      // out which one and subscribe to it.
      _upstreamClientOrSessionDisposer = credentials.AcceptVisitor(
        (CoreEndpointConfig _) => {
          var fobs = new FreshnessObserver<RefCounted<Client>>(this, _freshness.Current);
          return _stateManager.SubscribeToCoreClient(_endpointId, fobs);
        },
        (CorePlusEndpointConfig _) => {
          var fobs = new FreshnessObserver<RefCounted<SessionManager>>(this, _freshness.Current);
          return _stateManager.SubscribeToSessionManager(_endpointId, fobs);
        });
    }
  }

  void IStatusObserver<RefCounted<Client>>.OnStatus(string status) {
    OnNextHelper(status);
  }

  public void OnNext(RefCounted<Client> client) {
    OnNextHelper(ConnectionOkString);
  }

  void IStatusObserver<RefCounted<SessionManager>>.OnStatus(string status) {
    OnNextHelper(status);
  }

  public void OnNext(RefCounted<SessionManager> sm) {
    OnNextHelper(ConnectionOkString);
  }

  private void OnNextHelper(string message) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      SorUtil.ReplaceAndNotify(ref _endpointHealth, message, _observers);
    }
  }
}
