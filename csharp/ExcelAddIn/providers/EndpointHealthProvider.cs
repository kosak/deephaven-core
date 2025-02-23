using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Auth;

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
  IObserver<StatusOr<EndpointConfigBase>>,
  IObserver<StatusOr<Client>>,
  IObserver<StatusOr<SessionManager>>,
  IObservable<StatusOr<EndpointHealth>> {
  private const string UnsetHealthString = "[No Config]";
  private const string ConnectionOkString = "OK";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private IDisposable? _upstreamConfigSubDisposer = null;
  private IDisposable? _upstreamClientOrSessionSubDisposer = null;
  private StatusOr<EndpointHealth> _endpointHealth = UnsetHealthString;
  private readonly ObserverContainer<StatusOr<EndpointHealth>> _observers = new();

  public EndpointHealthProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to connection health changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<EndpointHealth>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _endpointHealth);
      if (isFirst) {
        _upstreamConfigSubDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<EndpointHealth>> observer) {
    lock (_sync) {
      _observers.RemoveAndWait(observer, out var isLast);
      if (!isLast) {
        return;
      }

      // Do these teardowns synchronously.
      Utility.Exchange(ref _upstreamConfigSubDisposer, null)?.Dispose();
      Utility.Exchange(ref _upstreamClientOrSessionSubDisposer, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(Utility.Exchange(ref _endpointHealth, UnsetHealthString));
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        // Upstream has status text.
        _observers.SetStateAndNotify(ref _endpointHealth, status);
        return;
      }

      _observers.SetStateAndNotify(ref _endpointHealth, "[Unknown]");
      Utility.Exchange(ref _upstreamClientOrSessionSubDisposer, null)?.Dispose();

      // Upstream has core or corePlus value. Use the visitor to figure 
      // out which one and subscribe to it.
      _upstreamClientOrSessionSubDisposer = cbase.AcceptVisitor(
        (CoreEndpointConfig _) => _stateManager.SubscribeToCoreClient(_endpointId, this),
        (CorePlusEndpointConfig _) => _stateManager.SubscribeToCorePlusSession(_endpointId, this));
    }
  }

  public void OnNext(StatusOr<Client> client) {
    lock (_sync) {
      // If valid value, then we notify with the ConnectionOKString. Otherwise we pass through
      // the status.
      var message = client.AcceptVisitor(_ => ConnectionOkString, s => s);
      _observers.SetStateAndNotify(ref _endpointHealth, message);
    }
  }

  public void OnNext(StatusOr<SessionManager> sm) {
    lock (_sync) {
      // If valid value, then we notify with the ConnectionOKString. Otherwise we pass through
      // the status.
      var message = sm.AcceptVisitor(_ => ConnectionOkString, s => s);
      _observers.SetStateAndNotify(ref _endpointHealth, message);
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
