﻿using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.DheClient.Session;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryProvider :
  IObserver<StatusOr<SessionBase>>, IObservable<StatusOr<Client>> {

  public static PersistentQueryProvider Create(EndpointId endpointId, PersistentQueryId? pqId,
    StateManager sm, Action onDispose) {

    var result = new PersistentQueryProvider(pqId, sm.WorkerThread, onDispose);
    var usd = sm.SubscribeToSession(endpointId, result);
    result._upstreamSubscriptionDisposer = usd;

    return result;
  }

  private readonly PersistentQueryId? _pqId;
  private readonly WorkerThread _workerThread;
  private Action? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();
  private StatusOr<Client> _client = StatusOr<Client>.OfStatus("[No Client]");

  public PersistentQueryProvider(PersistentQueryId? pqId, WorkerThread workerThread, Action onDispose) {
    _pqId = pqId;
    _workerThread = workerThread;
    _onDispose = onDispose;
  }

  public IDisposable Subscribe(IObserver<StatusOr<Client>> observer) {
    _workerThread.Invoke(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_client);
    });

    return _workerThread.InvokeWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeClientState();
    });
  }

  public void OnNext(StatusOr<SessionBase> sessionBase) {
    if (_workerThread.InvokeIfRequired(() => OnNext(sessionBase))) {
      return;
    }

    DisposeClientState();

    // If the new state is just a status message, make that our state and transmit to our observers
    if (!sessionBase.GetValueOrStatus(out var sb, out var status)) {
      _observers.SetAndSendStatus(ref _client, status);
      return;
    }

    // It's a real Session so start fetching it. Also do some validity checking on the PQ id.
    _ = sb.Visit(
      core => {
        var result = _pqId == null
          ? StatusOr<Client>.OfValue(core.Client)
          : StatusOr<Client>.OfStatus("Community Core cannot connect to PQ \"{pqId}\"");
        // Community Core either has a message or a value, so we are done.
        _observers.SetAndSend(ref _client, result);
        return Unit.Instance;
      },
      corePlus => {
        if (_pqId == null) {
          _observers.SetAndSendStatus(ref _client, "Enterprise Core+ requires a PQ to be specified");
          return Unit.Instance;
        }

        _observers.SetAndSendStatus(ref _client, "Attaching to PQ \"{pqId}\"");
        Utility.RunInBackground(() => PerformAttachInBackground(corePlus.SessionManager, _pqId));
        return Unit.Instance;
      });
  }

  public void PerformAttachInBackground(SessionManager sm, PersistentQueryId pqId) {
    StatusOr<Client> result;
    try {
      var dndClient = sm.ConnectToPqByName(pqId.Id, false);
      result = StatusOr<Client>.OfValue(dndClient);
    } catch (Exception ex) {
      result = StatusOr<Client>.OfStatus(ex.Message);
    }

    // Then, back on the worker thread, set the result
    _workerThread.Invoke(() => _observers.SetAndSend(ref _client, result));
  }

  private void DisposeClientState() {
    if (_workerThread.InvokeIfRequired(DisposeClientState)) {
      return;
    }

    _ = _client.GetValueOrStatus(out var oldClient, out _);
    _observers.SetAndSendStatus(ref _client, "Disposing Client");

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
