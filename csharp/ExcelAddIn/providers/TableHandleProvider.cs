using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableHandleProvider :
  IObserver<StatusOr<Client>>, IObservable<StatusOr<TableHandle>> {

  public static TableHandleProvider Create(TableTriple descriptor,
    StateManager sm, Action onDispose) {

    var result = new TableHandleProvider(descriptor.TableName, sm.WorkerThread, onDispose);
    // If endpointId is specified, then subscribe to the upstream PQ.
    // Otherwise (if not specified), don't bother subscribing.
    if (descriptor.EndpointId != null) {
      var usd = sm.SubscribeToPersistentQuery(descriptor.EndpointId, descriptor.PersistentQueryId, result);
      result._upstreamSubscriptionDisposer = usd;
    }

    return result;
  }

  private readonly string _tableName;
  private readonly WorkerThread _workerThread;
  private Action? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOr<TableHandle> _tableHandle = StatusOr<TableHandle>.OfStatus("[No Table]");

  public TableHandleProvider(string tableName, WorkerThread workerThread, Action onDispose) {
    _tableName = tableName;
    _workerThread = workerThread;
    _onDispose = onDispose;
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
      Utility.RunInBackground666(oldTh.Dispose);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
