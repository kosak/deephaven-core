using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.DheClient.Session;
using System.Windows.Forms;

namespace Deephaven.ExcelAddIn.Providers;

internal class ConnectionHealthProvider :
  IObserver<StatusOr<CredentialsBase>>,
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

  public void OnNext(StatusOr<CredentialsBase> credentials) {
    if (_workerThread.EnqueueOrNop(() => OnNext(credentials))) {
      return;
    }

    UnsubscribeClientOrSession();

    if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
      _observers.SetAndSendStatus(ref _connectionHealth, status);
      return;
    }

    _upstreamClientOrSessionSubDisposer = cbase.AcceptVisitor(
      core => _stateManager.SubscribeToCoreClient(core),
      corePlus => _stateManager.SubscribeToCorePlusSession(corePlus));
  }

  public void OnNext(StatusOr<Client> client) {
    if (_workerThread.EnqueueOrNop(() => OnNext(client))) {
      return;
    }

    var message = client.AcceptVisitor(_ => ConnectionOkString, s => s);
    _observers.SetAndSendStatus(ref _connectionHealth, message);
  }

  public void OnNext(StatusOr<SessionManager> sm) {
    if (_workerThread.EnqueueOrNop(() => OnNext(sm))) {
      return;
    }

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
