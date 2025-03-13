using System.CodeDom.Compiler;
using System.Diagnostics;
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
  IValueObserver<StatusOr<RefCounted<TableHandle>>>,
  // IValueObservable<StatusOr<RefCounted<TableHandle>>>,
  // IDisposable
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Filtered Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private readonly FreshnessTokenSource _freshnessSource;
  private readonly Latch _subscribeDone = new();
  private readonly Latch _isDisposed = new();
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
    _freshnessSource = new(_sync);
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _filteredTableHandle, out _);
      if (_subscribeDone.TrySet()) {
        // Subscribe to parent at the first-ever subscribe
        var tq = new TableQuad(_endpointId, _pqName, _tableName, "");
        Debug.WriteLine($"FilteredTableProvider is subscribing to TableHandle with {tq}");
        _upstreamDisposer = _stateManager.SubscribeToTable(tq, this);
      }
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _filteredTableHandle, "[Disposed");
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> parentHandle) {
    if (_executor.MaybeDefer(OnNext, parentHandle)) {
      return;
    }
    if (_isDisposed.Value) {
      return;
    }

    // Invalidate any outstanding background work
    var token = _freshnessSource.Refresh();

    if (!parentHandle.GetValueOrStatus(out var ph, out var status)) {
      StatusOrUtil.ReplaceAndNotify(ref _filteredTableHandle, status, _observers);
      return;
    }

    StatusOrUtil.ReplaceAndNotify(ref _filteredTableHandle, "Filtering", _observers);
    Background.Run(OnNextBackground, parentHandle, token);
  }

  private static void OnNextBackground(RefCounted<TableHandle> parentHandle,
    FreshnessToken token) {
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

    OnNextFinish(ref _filteredTableHandle, _newResuilt, _observers);

    lock (_sync) {
      if (token.IsCurrent) {
        StatusOrUtil.ReplaceAndNotify(ref _filteredTableHandle, newResult, _observers);
      }
    }
  }

  private void OnNextFinish(StatusOr<RefCounted<TableHandle>> result) {
    if (_executor.MaybeDefer(OnNextFinish, result)) {
      return;
    }






  }
}
