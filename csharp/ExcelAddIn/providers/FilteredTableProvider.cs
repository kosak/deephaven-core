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
  IStatusObserver<RefCounted<TableHandle>>,
  // IStatusObservable<RefCounted<TableHandle>>,
  // IDisposable
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Filtered Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private readonly Latch _subscribeDone = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<RefCounted<TableHandle>> _observers = new();
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<RefCounted<TableHandle>> _filteredTableHandle = UnsetTableHandleText;

  public FilteredTableProvider(StateManager stateManager, EndpointId endpointId,
    PqName? pqName, string tableName, string condition) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
    _condition = condition;
  }

  public IDisposable Subscribe(IStatusObserver<RefCounted<TableHandle>> observer) {
    lock (_sync) {
      SorUtil.AddObserverAndNotify(_observers, observer, _filteredTableHandle, out _);
      if (_subscribeDone.TrySet()) {
        // Subscribe to parent at the first-ever subscribe
        var tq = new TableQuad(_endpointId, _pqName, _tableName, "");
        Debug.WriteLine($"FilteredTableProvider is subscribing to TableHandle with {tq}");
        _upstreamDisposer = _stateManager.SubscribeToTable(tq, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IStatusObserver<RefCounted<TableHandle>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _upstreamDisposer);
      SorUtil.Replace(ref _filteredTableHandle, "[Disposed");
    }
  }

  public void OnStatus(string status) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Invalidate any outstanding background work
      _ = _versionTracker.New();
      SorUtil.ReplaceAndNotify(ref _filteredTableHandle, status, _observers);
    }
  }

  public void OnNext(RefCounted<TableHandle> parentHandle) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Invalidate any outstanding background work
      var cookie = _versionTracker.New();

      SorUtil.ReplaceAndNotify(ref _filteredTableHandle, "Filtering", _observers);
      // Share the handle (outside the lambda) so it can be owned by the separate thread.
      var parentHandleShare = parentHandle.Share();
      Background.Run(() => OnNextBackground(parentHandleShare, cookie));
    }
  }

  private void OnNextBackground(RefCounted<TableHandle> parentHandleShare,
    VersionTracker.Cookie versionCookie) {
    // This thread owns parentHandleShare so it needs to dispose it on the way out
    using var cleanup1 = parentHandleShare;
    RefCounted<TableHandle>? newRef = null;
    StatusOr<RefCounted<TableHandle>> newResult;
    try {
      // This is a server call that may take some time.
      var childHandle = parentHandleShare.Value.Where(_condition);
      newRef = RefCounted.Acquire(childHandle);
      newResult = newRef;
    } catch (Exception ex) {
      newResult = ex.Message;
    }
    // Dispose newRef on the way out (but the ReplaceAndNotify below will typically
    // share it with _filteredTableHandle).
    using var cleanup2 = newRef;

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        SorUtil.ReplaceAndNotify(ref _filteredTableHandle, newResult, _observers);
      }
    }
  }
}
