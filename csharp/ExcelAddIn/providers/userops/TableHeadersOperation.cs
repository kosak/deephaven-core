using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Refcounting;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableHeadersOperation :
  IValueObserverWithCancel<StatusOr<TableHandle>>,
  IValueObservable<StatusOr<object?[,]>> {
  private const string UnsetTableHandle = "No TableHandle";
  private const string UnsetTableData = "No headers";
  private readonly TableTriple _tableTriple;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IObservableCallbacks? _upstreamCallbacks = null;
  private readonly StatusOrHolder<TableHandle> _cachedTableHandle = new(UnsetTableHandle);
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private readonly StatusOrHolder<object?[,]> _rendered = new(UnsetTableData);

  public TableHeadersOperation(TableTriple tableTriple, StateManager stateManager) {
    _tableTriple = tableTriple;
    _stateManager = stateManager;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<object?[,]>> observer) {
    lock (_sync) {
      _rendered.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        if (_tableTriple.EndpointId != null) {
          _stateManager.EnsureConfig(_tableTriple.EndpointId);
        }
        var tq = new TableQuad(_tableTriple.EndpointId, _tableTriple.PqName,
          _tableTriple.TableName, "");
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamCallbacks = _stateManager.SubscribeToTable(tq, voc);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    lock (_sync) {
      // Ask the parent to Retry, regardless of whether our cached TableHandle is
      // healthy or in an error state.
      _upstreamCallbacks?.Retry();
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<object?[,]>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamCallbacks);
      _cachedTableHandle.Replace(UnsetTableHandle);
      _rendered.Replace(UnsetTableData);
    }
  }

  public void OnNext(StatusOr<TableHandle> tableHandle,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      _cachedTableHandle.Replace(tableHandle);
      OnNextHelper();
    }
  }

  private void OnNextHelper() {
    lock (_sync) {
      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      if (!_cachedTableHandle.GetValueOrStatus(out var th, out var status)) {
        _rendered.ReplaceAndNotify(status, _observers);
        return;
      }

      var progress = StatusOr<object?[,]>.OfTransient("[Rendering]");
      _rendered.ReplaceAndNotify(progress, _observers);

      // RefCounted item gets acquired on this thread.
      var sharedDisposer = Repository.Share(th);
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        // RefCounted item gets released on this thread.
        using var cleanup = sharedDisposer;
        OnNextBackground(th, backgroundToken);
      });
    }
  }

  private void OnNextBackground(TableHandle tableHandle, CancellationToken token) {
    StatusOr<object?[,]> newResult;
    try {
      // This is a server call that may take some time.
      using var ct = tableHandle.ToClientTable();
      newResult = Renderer.RenderHeaders(ct);
    } catch (Exception ex) {
      newResult = ex.Message;
    }

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _rendered.ReplaceAndNotify(newResult, _observers);
    }
  }
}
