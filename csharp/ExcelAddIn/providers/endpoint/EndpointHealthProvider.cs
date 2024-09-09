using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
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
/// We use StatusOr&lt;ConnectionHealth&gt; . A healthy connection sends a StatusOr
/// with value set to ConnectionHealth (this is an object without any members). On the
/// other hand, an unhealthy connection sends a StatusOr with the status text sent to
/// whatever status text was received from upstream.
/// </summary>
internal class EndpointHealthProvider :
  IValueObserverWithCancel<StatusOr<EndpointConfigBase>>,
  IValueObserverWithCancel<StatusOr<Client>>,
  IValueObserverWithCancel<StatusOr<SessionManager>>,
  IValueObservable<StatusOr<EndpointHealth>> {
  private const string UnsetHealthString = "Unknown health";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _clientOrSessionTokenSource = new();
  private IDisposable? _upstreamConfigDisposer = null;
  private IDisposable? _upstreamClientOrSessionDisposer = null;
  private readonly ObserverContainer<StatusOr<EndpointHealth>> _observers = new();
  private readonly StatusOrHolder<EndpointHealth> _endpointHealth = new(UnsetHealthString);

  public EndpointHealthProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to connection health changes
  /// </summary>
  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<EndpointHealth>> observer) {
    lock (_sync) {
      _endpointHealth.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create<StatusOr<EndpointConfigBase>>(
          this, _upstreamTokenSource.Token);
        _upstreamConfigDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, voc);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    // Nothing to do for Retry.
  }

  private void RemoveObserver(IValueObserver<StatusOr<EndpointHealth>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamConfigDisposer);
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);
      _endpointHealth.Replace(UnsetHealthString);
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Invalidate inflight clientOrSession messages and unsubscribe
      _clientOrSessionTokenSource.Cancel();
      _clientOrSessionTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
        _upstreamTokenSource.Token);
      Utility.ClearAndDispose(ref _upstreamClientOrSessionDisposer);

      if (!credentials.GetValueOrStatus(out var creds, out var status)) {
        _endpointHealth.ReplaceAndNotify(status, _observers);
        return;
      }

      _endpointHealth.ReplaceAndNotify(UnsetHealthString, _observers);

      // Upstream has core or corePlus value. Use the visitor to figure 
      // out which one and subscribe to it.

      _upstreamClientOrSessionDisposer = creds.AcceptVisitor<IDisposable?>(
        (EmptyEndpointConfig _) => {
          _endpointHealth.ReplaceAndNotify("Endpoint is empty", _observers);
          return null;
        },
        (CoreEndpointConfig _) => {
          var voc = ValueObserverWithCancelWrapper.Create<StatusOr<Client>>(
            this, _upstreamTokenSource.Token);
          return _stateManager.SubscribeToCoreClient(_endpointId, voc);
        },
        (CorePlusEndpointConfig _) => {
          var voc = ValueObserverWithCancelWrapper.Create<StatusOr<SessionManager>>(
            this, _upstreamTokenSource.Token);
          return _stateManager.SubscribeToSessionManager(_endpointId, voc);
        });
    }
  }

  public void OnNext(StatusOr<Client> client, CancellationToken token) {
    OnNextHelper(client, token);
  }

  public void OnNext(StatusOr<SessionManager> sessionManager,
    CancellationToken token) {
    OnNextHelper(sessionManager, token);
  }

  private void OnNextHelper<T>(StatusOr<T> statusOr, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOr<EndpointHealth> result;
      if (statusOr.GetValueOrStatus(out _, out var status)) {
        result = new EndpointHealth();
      } else {
        result = status;
      }
      _endpointHealth.ReplaceAndNotify(result, _observers);
    }
  }
}
