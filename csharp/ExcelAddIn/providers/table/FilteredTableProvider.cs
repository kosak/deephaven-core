using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/**
 * The job of this class is to subscribe to a table provider with the key
 * (endpoint, pqName, tableName, condition). Then, as that table provider provides me
 * with TableHandles or status messages, process them.
 *
 * If the message received was a status message, then forward it to my observers.
 * If it was a TableHandle, then filter it by "condition" in the background, and provide
 * the resulting filtered TableHandle (or error) to my observers.
 */
internal class FilteredTableProvider :
  IValueObserverWithCancel<StatusOr<RefCounted<TableHandle>>>,
  // IValueObservable<StatusOr<RefCounted<TableHandle>>>,
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Filtered Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _filteredTableHandle = UnsetTableHandleText;

  public FilteredTableProvider(StateManager stateManager, EndpointId endpointId,
    PqName? pqName, string tableName, string condition) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
    _condition = condition;
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _filteredTableHandle,
        out var isFirst);
      if (isFirst) {
        var tq = new TableQuad(_endpointId, _pqName, _tableName, "");
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamDisposer = _stateManager.SubscribeToTable(tq, voc);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _filteredTableHandle, UnsetTableHandleText);
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> parentHandle, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      if (!parentHandle.GetValueOrStatus(out var ph, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _filteredTableHandle, status, _observers);
        return;
      }

      StatusOrUtil.ReplaceAndNotify(ref _filteredTableHandle, "Filtering", _observers);

      // RefCounted item gets acquired on this thread.
      var phShare = ph.Share();
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        // RefCounted item gets released on this thread.
        using var cleanup = phShare;
        OnNextBackground(phShare, backgroundToken);
      });
    }
  }

  private void OnNextBackground(RefCounted<TableHandle> parentHandle,
    CancellationToken token) {
    RefCounted<TableHandle>? newRef = null;
    StatusOr<RefCounted<TableHandle>> newResult;
    try {
      // This is a server call that may take some time.
      var childHandle = parentHandle.Value.Where(_condition);
      // The child handle takes a dependency on the parent handle, not because
      // TableHandles have a dependency on each other, but because the parent handle
      // has a transitive dependency on the Client or DndClient, and that's what
      // we need to keep alive while we're alive.
      newRef = RefCounted.Acquire(childHandle, parentHandle);
      newResult = newRef;
    } catch (Exception ex) {
      newResult = ex.Message;
    }
    using var cleanup = newRef;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _filteredTableHandle, newResult, _observers);
    }
  }
}
