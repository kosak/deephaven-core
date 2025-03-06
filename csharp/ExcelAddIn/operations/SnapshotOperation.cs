using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using ExcelDna.Integration;
using System.Reflection.Metadata.Ecma335;

namespace Deephaven.ExcelAddIn.Operations;

internal class SnapshotOperation : IExcelObservable, IStatusObserver<RefCounted<TableHandle>> {
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<object?[,]> _rendered = "[No data]";

  public SnapshotOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    var wrappedObserver = ExcelDnaHelpers.WrapExcelObserver(observer);

    lock (_sync) {
      _observers.AddAndNotify(wrappedObserver, _rendered, out var isFirst);

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
      ProviderUtil.SetState(ref _rendered, "[Disposed]");
    }
  }

  public void OnNext(string status) {
    lock (_sync) {
      // Invalidate any outstanding background work
      _ = _versionTracker.New();
      ProviderUtil.SetStateAndNotify(ref _rendered, status, _observers);
    }
  }

  public void OnNext(RefCounted<TableHandle> tableHandle) {
    lock (_sync) {
      // Invalidate any outstanding background work
      var cookie = _versionTracker.New();

      ProviderUtil.SetStateAndNotify(ref _rendered, "[Rendering]", _observers);

      // This needs to be created early (not on the lambda, which is on a different thread)
      var tableHandleShare = tableHandle.Share();
      Background.Run(() => OnNextBackground(tableHandleShare, cookie));
    }
  }

  private void OnNextBackground(RefCounted<TableHandle> tableHandleShare,
    VersionTracker.Cookie versionCookie) {
    using var cleanup1 = tableHandleShare;
    StatusOr<object?[,]> newResult;
    try {
      // This is a server call that may take some time.
      using var ct = th.Value.ToClientTable();
      var rendered = Renderer.Render(ct, _wantHeaders);
      newResult = StatusOr<object?[,]>.OfValue(rendered);
    } catch (Exception ex) {
      newResult = ex.Message;
    }
    using var cleanup2 = newResult;

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        ProviderUtil.SetStateAndNotify(ref _rendered, newResult, _observers);
      }
    }
  }
}
