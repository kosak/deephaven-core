using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.DheClient.Session;

namespace Deephaven.ExcelAddIn.Providers;

/**
 * Observes the ConnectionConfig. When a valid ConnectionConfig is received, observes
 * the appropriate CoreClientProvider or CorePlusSessionProvider.
 * When those things provide responses, translate them to ConnectionHealth messages.
 * We use StatusOr&lt;ConnectionHealth&gt; .  A healthy connection sends a StatusOr
 * with value set to ConnectionHealth (this is an object without any members). On the
 * other hand, an unhealthy connection sends a StatusOr with the status text sent to
 * whatever status text was received from upstream.
 */
internal class EndpointHealthProvider :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObserver<StatusOr<Client>>,
  IObserver<StatusOr<SessionManager>>,
  IObservable<StatusOr<EndpointHealth>> {
  private const string ConnectionOkString = "OK";

  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly EndpointId _endpointId;
  private Action? _onDispose;
  private IDisposable? _upstreamConfigSubDisposer = null;
  private IDisposable? _upstreamClientOrSessionSubDisposer = null;
  private StatusOr<EndpointHealth> _endpointHealth = StatusOr<EndpointHealth>.OfStatus("[No config]");
  private readonly ObserverContainer<StatusOr<EndpointHealth>> _observers = new();

  public EndpointHealthProvider(StateManager stateManager, EndpointId endpointId, Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _endpointId = endpointId;
    _onDispose = onDispose;
  }

  public void Init() {
    _upstreamConfigSubDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
  }

  /// <summary>
  /// Subscribe to connection health changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<EndpointHealth>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_endpointHealth);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamConfigSubDisposer, null)?.Dispose();
      UnsubscribeClientOrSession();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
    });
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    if (_workerThread.EnqueueOrNop(() => OnNext(credentials))) {
      return;
    }

    UnsubscribeClientOrSession();

    if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
      // Upstream has status text.
      _observers.SetAndSendStatus(ref _endpointHealth, status);
      return;
    }

    // Upstream has core or corePlus value. Use the visitor to figure 
    // out which one and subscribe to it.
    _upstreamClientOrSessionSubDisposer = cbase.AcceptVisitor(
      (CoreEndpointConfig _) => _stateManager.SubscribeToCoreClient(_endpointId, this),
      (CorePlusEndpointConfig _) => _stateManager.SubscribeToCorePlusSession(_endpointId, this));
  }

  public void OnNext(StatusOr<Client> client) {
    if (_workerThread.EnqueueOrNop(() => OnNext(client))) {
      return;
    }

    // If valid value, then we use the ConnectionOKString (something like "OK").
    // Otherwise, we pass through the status.
    var message = client.AcceptVisitor(_ => ConnectionOkString, s => s);
    _observers.SetAndSendStatus(ref _endpointHealth, message);
  }

  public void OnNext(StatusOr<SessionManager> sm) {
    if (_workerThread.EnqueueOrNop(() => OnNext(sm))) {
      return;
    }

    // If valid value, then we use the ConnectionOKString (something like "OK").
    // Otherwise, we pass through the status.
    var message = sm.AcceptVisitor(_ => ConnectionOkString, s => s);
    _observers.SetAndSendStatus(ref _endpointHealth, message);
  }

  private void UnsubscribeClientOrSession() {
    if (_workerThread.EnqueueOrNop(UnsubscribeClientOrSession)) {
      return;
    }

    Utility.Exchange(ref _upstreamClientOrSessionSubDisposer, null)?.Dispose();
    _observers.SetAndSendStatus(ref _endpointHealth, "[Unknown]");
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
