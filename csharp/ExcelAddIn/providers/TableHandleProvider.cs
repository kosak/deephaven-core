using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using System.Diagnostics;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableHandleProvider :
  IObserver<StatusOr<Client>>,
  IObserver<EndpointId?>,
  IObservable<StatusOr<TableHandle>> {
  private const string UnsetTableHandleText = "[No Table]";

  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly TableTriple _descriptor;
  private Action? _onDispose;
  private IDisposable? _endpointSubscriptionDisposer = null;
  private IDisposable? _pqSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOr<TableHandle> _tableHandle = StatusOr<TableHandle>.OfStatus(UnsetTableHandleText);

  public TableHandleProvider(StateManager stateManager, TableTriple descriptor,
    Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _descriptor = descriptor;
    _onDispose = onDispose;
  }

  public void Init() {
    // If we have an endpointId, subscribe directly to the PQ
    if (_descriptor.EndpointId != null) {
      _pqSubscriptionDisposer = _stateManager.SubscribeToPersistentQuery(
        _descriptor.EndpointId, _descriptor.PersistentQueryId, this);
      return;
    }

    // If we don't, that means the caller wanted the default endpoint, so subscribe
    // to the observable for default endpoints. When we get one, then we can subscribe
    // to a PQ.
    _endpointSubscriptionDisposer = _stateManager.SubscribeToDefaultEndpointSelection(this);
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

      Utility.Exchange(ref _endpointSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _pqSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeTableHandleState();
    });
  }

  public void OnNext(EndpointId? endpointId) {
    // Unsubscribe from old PQs
    Utility.Exchange(ref _pqSubscriptionDisposer, null)?.Dispose();

    // Forget TableHandleState
    DisposeTableHandleState();

    // If endpoint is null, then don't subscribe to anything.
    if (endpointId == null) {
      return;
    }

    Debug.WriteLine($"TH is subscribing to PQ ({endpointId}, {_descriptor.PersistentQueryId})");
    _pqSubscriptionDisposer = _stateManager.SubscribeToPersistentQuery(
      endpointId, _descriptor.PersistentQueryId, this);
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
    _observers.SetAndSendStatus(ref _tableHandle, $"Fetching \"{_descriptor.TableName}\"");

    try {
      var th = cli.Manager.FetchTable(_descriptor.TableName);
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
    _observers.SetAndSendStatus(ref _tableHandle, UnsetTableHandleText);

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
