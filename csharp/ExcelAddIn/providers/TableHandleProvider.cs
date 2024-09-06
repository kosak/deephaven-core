using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using System.Diagnostics;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableHandleProvider :
  IObserver<StatusOr<Client>>,
  IObserver<EndpointId?>,
  IObservable<StatusOr<TableHandle>> {

  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private Action? _onDispose;
  private IDisposable? _upstreamEndpointDisposer = null;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOr<TableHandle> _tableHandle = StatusOr<TableHandle>.OfStatus("[No Table]");

  public TableHandleProvider(StateManager stateManager, PersistentQueryId? persistentQueryId, string tableName,
    Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _onDispose = onDispose;
  }

  public void Init(StateManager sm) {
    _upstreamEndpointDisposer = sm.SubscribeToDefaultEndpointSelection(this);
  }

  public void OnNext(EndpointId? endpointId) {
    // Unsubscribe from old dependencies
    Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();

    // Forget TableHandleState
    DisposeTableHandleState();

    // If endpoint is null, then don't subscribe to anything.
    if (endpointId == null) {
      return;
    }

    Debug.WriteLine($"TH is subscribing to PQ ({endpointId}, {_persistentQueryId})");
    _upstreamSubscriptionDisposer = _stateManager.SubscribeToPersistentQuery(
      endpointId, _persistentQueryId, this);
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    _workerThread.Invoke(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_tableHandle);
    });

    return _workerThread.InvokeWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamEndpointDisposer, null)?.Dispose();
      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeTableHandleState();
    });
  }

  public void OnNext(StatusOr<Client> client) {
    if (_workerThread.InvokeIfRequired(() => OnNext(client))) {
      return;
    }

    DisposeTableHandleState();

    // If the new state is just a status message, make that our state and transmit to our observers
    if (!client.GetValueOrStatus(out var cli, out var status)) {
      _observers.SetAndSendStatus(ref _tableHandle, status);
      return;
    }

    // It's a real client so start fetching the table. First notify our observers.
    _observers.SetAndSendStatus(ref _tableHandle, $"Fetching \"{_tableName}\"");

    try {
      var th = cli.Manager.FetchTable(_tableName);
      _observers.SetAndSendValue(ref _tableHandle, th);
    } catch (Exception ex) {
      _observers.SetAndSendStatus(ref _tableHandle, ex.Message);
    }
  }

  private void DisposeTableHandleState() {
    if (_workerThread.InvokeIfRequired(DisposeTableHandleState)) {
      return;
    }

    _ = _tableHandle.GetValueOrStatus(out var oldTh, out _);
    _observers.SetAndSendStatus(ref _tableHandle, "Disposing TableHandle");

    if (oldTh != null) {
      Utility.RunInBackground(oldTh.Dispose);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
