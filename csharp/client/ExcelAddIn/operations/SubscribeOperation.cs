using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.Operations;

internal class SubscribeOperation : IExcelObservable, IObserver<StatusOr<TableHandle>> {
  private readonly TableDescriptor _tableDescriptor;
  private readonly string _filter;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private readonly WorkerThread _workerThread;
  private IDisposable? _filteredTableDisposer = null;
  private TableHandle? _currentTableHandle = null;
  private SubscriptionHandle? _currentSubHandle = null;

  public SubscribeOperation(TableDescriptor tableDescriptor, string filter, bool wantHeaders,
    StateManager stateManager) {
    _tableDescriptor = tableDescriptor;
    _filter = filter;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
    // Convenience
    _workerThread = _stateManager.WorkerThread;
  }

  IDisposable IExcelObservable.Subscribe(IExcelObserver observer) {
    var wrappedObserver = new ZamboniWrapper(observer);
    _workerThread.Invoke(() => {
      _observers.Add(wrappedObserver, out var isFirst);

      if (isFirst) {
        _filteredTableDisposer = _stateManager.Subscribe(_tableDescriptor, _filter, this);
      }
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        _observers.Remove(wrappedObserver, out var wasLast);
        if (!wasLast) {
          return;
        }

        var temp = _filteredTableDisposer;
        _filteredTableDisposer = null;
        temp?.Dispose();
      });
    });
  }

  void IObserver<StatusOr<TableHandle>>.OnNext(StatusOr<TableHandle> soth) {
    _workerThread.Invoke(() => {
      // First tear down old state
      if (_currentTableHandle != null) {
        _currentTableHandle.Unsubscribe(_currentSubHandle!);
        _currentSubHandle!.Dispose();
        _currentTableHandle = null;
        _currentSubHandle = null;
      }

      if (!soth.TryGetValue(out var tableHandle, out var status)) {
        _observers.SendStatusAll(status);
        return;
      }

      _observers.SendStatusAll($"Subscribing to \"{_tableDescriptor.TableName}\"");

      _currentTableHandle = tableHandle;
      _currentSubHandle = _currentTableHandle.Subscribe(new MyTickingCallback(_observers, _wantHeaders));

      try {
        using var ct = tableHandle.ToClientTable();
        var result = Renderer.Render(ct, _wantHeaders);
        _observers.SendValueAll(result);
      } catch (Exception ex) {
        _observers.SendStatusAll(ex.Message);
      }
    });
  }

  void IObserver<StatusOr<TableHandle>>.OnCompleted() {
    // TODO(kosak): TODO
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<TableHandle>>.OnError(Exception error) {
    // TODO(kosak): TODO
    throw new NotImplementedException();
  }

  private class MyTickingCallback : ITickingCallback {
    private readonly ObserverContainer<StatusOr<object?[,]>> _observers;
    private readonly bool _wantHeaders;

    public MyTickingCallback(ObserverContainer<StatusOr<object?[,]>> observers,
      bool wantHeaders) {
      _observers = observers;
      _wantHeaders = wantHeaders;
    }

    public void OnTick(TickingUpdate update) {
      try {
        var results = Renderer.Render(update.Current, _wantHeaders);
        _observers.SendValueAll(results);
      } catch (Exception e) {
        _observers.SendStatusAll(e.Message);
      }
    }

    public void OnFailure(string errorText) {
      _observers.SendStatusAll(errorText);
    }
  }
}
