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
internal class ConnectionHealthProvider :
  IObserver<StatusOr<ConnectionConfigBase>>,
  IObserver<StatusOr<Client>>,
  IObserver<StatusOr<SessionManager>>,
  IObservable<StatusOr<ConnectionHealth>> {
  private const string ConnectionOkString = "OK";

  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly EndpointId _endpointId;
  private Action? _onDispose;
  private IDisposable? _upstreamCredentialsSubDisposer = null;
  private IDisposable? _upstreamClientOrSessionSubDisposer = null;
  private StatusOr<ConnectionHealth> _connectionHealth = StatusOr<ConnectionHealth>.OfStatus("[No credentials]");
  private readonly ObserverContainer<StatusOr<ConnectionHealth>> _observers = new();

  public ConnectionHealthProvider(StateManager stateManager, EndpointId endpointId, Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _endpointId = endpointId;
    _onDispose = onDispose;
  }

  public void Init() {
    _upstreamCredentialsSubDisposer = _stateManager.SubscribeToCredentials(_endpointId, this);
  }

  /// <summary>
  /// Subscribe to connection health changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<ConnectionHealth>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_connectionHealth);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamCredentialsSubDisposer, null)?.Dispose();
      UnsubscribeClientOrSession();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
    });
  }

  public void OnNext(StatusOr<ConnectionConfigBase> credentials) {
    if (_workerThread.EnqueueOrNop(() => OnNext(credentials))) {
      return;
    }

    UnsubscribeClientOrSession();

    if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
      // Upstream has status text.
      _observers.SetAndSendStatus(ref _connectionHealth, status);
      return;
    }

    // Upstream has core or corePlus value. Use the visitor to figure 
    // out which one and subscribe to it.
    _upstreamClientOrSessionSubDisposer = cbase.AcceptVisitor(
      (CoreConnectionConfig _) => _stateManager.SubscribeToCoreClient(_endpointId, this),
      (CorePlusConnectionConfig _) => _stateManager.SubscribeToCorePlusSession(_endpointId, this));
  }

  public void OnNext(StatusOr<Client> client) {
    if (_workerThread.EnqueueOrNop(() => OnNext(client))) {
      return;
    }

    // If valid value, then we use the ConnectionOKString (something like "OK").
    // Otherwise, we pass through the status.
    var message = client.AcceptVisitor(_ => ConnectionOkString, s => s);
    _observers.SetAndSendStatus(ref _connectionHealth, message);
  }

  public void OnNext(StatusOr<SessionManager> sm) {
    if (_workerThread.EnqueueOrNop(() => OnNext(sm))) {
      return;
    }

    // If valid value, then we use the ConnectionOKString (something like "OK").
    // Otherwise, we pass through the status.
    var message = sm.AcceptVisitor(_ => ConnectionOkString, s => s);
    _observers.SetAndSendStatus(ref _connectionHealth, message);
  }

  private void UnsubscribeClientOrSession() {
    if (_workerThread.EnqueueOrNop(UnsubscribeClientOrSession)) {
      return;
    }

    Utility.Exchange(ref _upstreamClientOrSessionSubDisposer, null)?.Dispose();
    _observers.SetAndSendStatus(ref _connectionHealth, "[Unknown]");
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
