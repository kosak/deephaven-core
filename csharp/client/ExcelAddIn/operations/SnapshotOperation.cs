using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.Operations;

internal class SnapshotOperation : IExcelObservable, IObserver<StatusOr<TableHandle>> {
  private readonly TableDescriptor _tableDescriptor;
  private readonly string _filter;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private readonly WorkerThread _workerThread;
  private IDisposable? _filteredTableDisposer = null;

  public SnapshotOperation(TableDescriptor tableDescriptor, string filter, bool wantHeaders,
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
      if (!soth.TryGetValue(out var tableHandle, out var status)) {
        _observers.SendStatusAll(status);
        return;
      }

      _observers.SendStatusAll($"Snapshotting \"{_tableDescriptor.TableName}\"");

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

  private class ZamboniWrapper(IExcelObserver inner) : IObserver<StatusOr<object?[,]>> {
    public void OnNext(StatusOr<object?[,]> sov) {
      if (!sov.TryGetValue(out var value, out var status)) {
        value = new object[,] { { status } };
      }
      inner.OnNext(value);
    }

    public void OnCompleted() {
      inner.OnCompleted();
    }

    public void OnError(Exception error) {
      inner.OnError(error);
    }
  }
}
