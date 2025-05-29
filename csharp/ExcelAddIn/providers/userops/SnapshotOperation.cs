using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class SnapshotOperation : 
  IValueObserverWithCancel<StatusOr<RefCounted<TableHandle>>>,
  IValueObservable<StatusOr<object?[,]>> {
  private const string UnsetTableHandle = "[No TableHandle]";
  private const string UnsetTableData = "[No data]";
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IObservableCallbacks? _upstreamCallbacks = null;
  private StatusOr<RefCounted<TableHandle>> _cachedTableHandle = UnsetTableHandle;
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private StatusOr<object?[,]> _rendered = UnsetTableData;

  public SnapshotOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<object?[,]>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _rendered, out var isFirst);

      if (isFirst) {
        if (_tableQuad.EndpointId != null) {
          _stateManager.EnsureConfig(_tableQuad.EndpointId);
        }
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamCallbacks = _stateManager.SubscribeToTable(_tableQuad, voc);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    lock (_sync) {
      if (!_cachedTableHandle.GetValueOrStatus(out _, out _)) {
        _upstreamCallbacks?.Retry();
      } else {
        OnNextHelper();
      }
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
      StatusOrUtil.Replace(ref _cachedTableHandle, UnsetTableHandle);
      StatusOrUtil.Replace(ref _rendered, UnsetTableData);
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> tableHandle,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      StatusOrUtil.Replace(ref _cachedTableHandle, tableHandle);
      OnNextHelper();
    }
  }

  private void OnNextHelper() {
    lock (_sync) {
      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      if (!_cachedTableHandle.GetValueOrStatus(out var th, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _rendered, status, _observers);
        return;
      }

      var progress = StatusOr<object?[,]>.OfTransient("[Rendering]");
      StatusOrUtil.ReplaceAndNotify(ref _rendered, progress, _observers);

      // RefCounted item gets acquired on this thread.
      var thShare = th.Share();
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        // RefCounted item gets released on this thread.
        using var cleanup = thShare;
        OnNextBackground(thShare, backgroundToken);
      });
    }
  }

  private void OnNextBackground(RefCounted<TableHandle> tableHandle,
    CancellationToken token) {
    StatusOr<object?[,]> newResult;
    try {
      // This is a server call that may take some time.
      using var ct = tableHandle.Value.ToClientTable();
      newResult = Renderer.Render(ct, _wantHeaders);
    } catch (Exception ex) {
      newResult = ex.Message;
    }

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _rendered, newResult, _observers);
    }
  }
}
