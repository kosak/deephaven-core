using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryInfoProvider :
  IObserver<SharableDict<PersistentQueryInfoMessage>>,
  IObservable<StatusOr<PersistentQueryInfoMessage>> {
  private const string UnsetPqText = "[No Persistent Query]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly string _pqName;
  private readonly object _sync = new();
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<PersistentQueryInfoMessage>> _observers = new();
  private StatusOr<PersistentQueryInfoMessage> _infoMessage = UnsetPqText;

  public PersistentQueryInfoProvider(StateManager stateManager,
    EndpointId endpointId, string pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
  }

  public void Init() {
    _upstreamSubscriptionDisposer = _stateManager.SubscribeToCorePlusSession(_endpointId, this);
  }

  public IDisposable Subscribe(IObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _infoMessage);
      if (isFirst) {
        _upstreamSubscriptionDisposer = _stateManager.SubscribeToZamboni(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }

    // Do these teardowns synchronously, but not under lock.
    IDisposable? disp1;
    lock (_sync) {
      disp1 = Utility.Exchange(ref _upstreamSubscriptionDisposer, null);
    }
    disp1?.Dispose();
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
