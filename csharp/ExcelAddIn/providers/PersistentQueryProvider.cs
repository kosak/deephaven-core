using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryProvider :
  IObserver<StatusOr<SessionManager>>, IObservable<StatusOr<Client>> {

  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId _pqId;
  private Action? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();
  private StatusOr<Client> _client = StatusOr<Client>.OfStatus("[No Client]");
  private Client? _ownedDndClient = null;

  public PersistentQueryProvider(StateManager stateManager,
    EndpointId endpointId, PersistentQueryId pqId, Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _endpointId = endpointId;
    _pqId = pqId;
    _onDispose = onDispose;
  }

  public void Init() {
    _upstreamSubscriptionDisposer = _stateManager.SubscribeToCorePlusSession(_endpointId, this);
  }

  public IDisposable Subscribe(IObserver<StatusOr<Client>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_client);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeClientState();
    });
  }

  //        : StatusOr<Client>.OfStatus("PQ specified, but Community Core cannot connect to a PQ");
  //   _observers.SetAndSend(ref _client, result);
  //   return Unit.Instance;
  // },
  // corePlus => {
  //   if (_pqId == null) {
  //     _observers.SetAndSendStatus(ref _client, "Enterprise Core+ requires a PQ to be specified");

  public void OnNext(StatusOr<SessionManager> sessionManager) {
    if (_workerThread.EnqueueOrNop(() => OnNext(sessionManager))) {
      return;
    }

    DisposeClientState();

    // If the new state is just a status message, make that our state and transmit to our observers
    if (!sessionManager.GetValueOrStatus(out var sm, out var status)) {
      _observers.SetAndSendStatus(ref _client, status);
      return;
    }

    // It's a real Session so start fetching it. Also do some validity checking on the PQ id.
    _observers.SetAndSendStatus(ref _client, $"Attaching to \"{_pqId}\"");

    try {
      _ownedDndClient = sm.ConnectToPqByName(_pqId.Id, false);
      _observers.SetAndSendValue(ref _client, _ownedDndClient);
    } catch (Exception ex) {
      _observers.SetAndSendStatus(ref _client, ex.Message);
    }
  }

  private void DisposeClientState() {
    if (_workerThread.EnqueueOrNop(DisposeClientState)) {
      return;
    }

    _observers.SetAndSendStatus(ref _client, "Disposing Client");
    var oldClient = Utility.Exchange(ref _ownedDndClient, null);
    if (oldClient != null) {
      Utility.RunInBackground(oldClient.Dispose);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
