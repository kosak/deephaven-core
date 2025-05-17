using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.Operations;

internal class SnapshotOperation : 
  IValueObserver<StatusOr<RefCounted<TableHandle>>>,
  IValueObservable<StatusOr<object?[,]>> {
  private const string UnsetTableData = "[No data]";
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private readonly FreshnessTokenSource _freshness;
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private StatusOr<object?[,]> _rendered = UnsetTableData;

  public SnapshotOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
    _freshness = new(_sync);
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    lock (_sync) {
      var wrappedObserver = new ExcelObserverWrapper(observer);
      _observers.AddAndNotify(wrappedObserver, _rendered, out var isFirst);

      if (isFirst) {
        if (_tableQuad.EndpointId != null) {
          _stateManager.EnsureConfig(_tableQuad.EndpointId);
        }
        _upstreamDisposer = _stateManager.SubscribeToTable(_tableQuad, this);
      }
      return ActionAsDisposable.Create(() => RemoveObserver(wrappedObserver));
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<object?[,]>> wrappedObserver) {
    lock (_sync) {
      _observers.Remove(wrappedObserver, out var wasLast);
      if (!wasLast) {
        return;
      }

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _rendered, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> tableHandle) {
    lock (_sync) {
      // Invalidate any outstanding background work
      var token = _freshness.Refresh();

      if (!tableHandle.GetValueOrStatus(out var th, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _rendered, status, _observers);
        return;
      }

      StatusOrUtil.ReplaceAndNotify(ref _rendered, "[Rendering]", _observers);

      var thShare = th.Share();
      Background.Run(() => {
        using var cleanup = thShare;
        OnNextBackground(thShare, token);
      });
    }
  }

  private void OnNextBackground(RefCounted<TableHandle> tableHandle,
    FreshnessToken token) {
    StatusOr<object?[,]> newResult;
    try {
      // This is a server call that may take some time.
      using var ct = tableHandle.Value.ToClientTable();
      newResult = Renderer.Render(ct, _wantHeaders);
    } catch (Exception ex) {
      newResult = ex.Message;
    }

    lock (_sync) {
      if (!token.IsCurrent) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _rendered, newResult, _observers);
    }
  }
}
