using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.Operations;

internal class SnapshotOperation : IExcelObservable, IObserver<StatusOr<TableHandle>> {
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private IDisposable? _upstreamDisposer = null;

  public SnapshotOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    var wrappedObserver = ExcelDnaHelpers.WrapExcelObserver(observer);

    lock (_sync) {
      _observers.AddAndNotify(wrappedObserver, "[Snapshotting]", out var isFirst);

      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToTable(_tableQuad, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(wrappedObserver));
  }

  private void RemoveObserver(IObserver<StatusOr<object?[,]>> wrappedObserver) {
    lock (_sync) {
      _observers.Remove(wrappedObserver, out var wasLast);
      if (!wasLast) {
        return;
      }

      Utility.ClearAndDispose(ref _upstreamDisposer);
    }
  }S

  public void OnNext(StatusOr<TableHandle> tableHandle) {
    if (_workerThread.EnqueueOrNop(() => OnNext(tableHandle))) {
      return;
    }

    if (!tableHandle.GetValueOrStatus(out var th, out var status)) {
      _observers.SendStatus(status);
      return;
    }

    _observers.SendStatus($"Snapshotting \"{_tableQuad.TableName}\"");

    try {
      using var ct = th.ToClientTable();
      var rendered = Renderer.Render(ct, _wantHeaders);
      _observers.SendValue(rendered);
    } catch (Exception ex) {
      _observers.SendStatus(ex.Message);
    }
  }

  void IObserver<StatusOr<TableHandle>>.OnCompleted() {
    // TODO(kosak): TODO
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<TableHandle>>.OnError(Exception error) {
    // TODO(kosak): TODO
    throw new NotImplementedException();
  }
}
