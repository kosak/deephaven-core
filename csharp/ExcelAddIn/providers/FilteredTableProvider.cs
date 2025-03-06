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
  // IObservable<StatusOr<TableHandle>>,
  // IDisposable
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Filtered Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private Latch _isDisposed = new();
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
      RefUtil.AddAndNotify(_observers, observer, _filteredTableHandle, out var isFirst);
      if (isFirst) {
        // Subscribe to parent at the time of the first subscription.
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
      ProviderUtil.SetState(ref _filteredTableHandle, "[Disposed");
    }
  }

  public void OnStatus(string status) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Invalidate any outstanding background work
      _ = _versionTracker.New();
      ProviderUtil.SetStateAndNotify(ref _filteredTableHandle, status, _observers);
    }
  }

  public void OnNext(RefCounted<TableHandle> parentHandle) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Invalidate any outstanding background work
      var cookie = _versionTracker.New();

      ProviderUtil.SetStateAndNotify(ref _filteredTableHandle, "Filtering", _observers);
      // This needs to be created early (not on the lambda, which is on a different thread)
      var parentHandleShare = parentHandle.Share();
      Background.Run(() => OnNextBackground(parentHandleShare, cookie));
    }
  }

  private void OnNextBackground(RefCounted<TableHandle> parentHandleShare,
    VersionTracker.Cookie versionCookie) {
    using var cleanup1 = parentHandleShare;
    StatusOr<RefCounted<TableHandle>> newResult;
    try {
      // This is a server call that may take some time.
      var childHandle = parentHandleShare.Value.Where(_condition);
      newResult = RefCounted.Acquire(childHandle);
    } catch (Exception ex) {
      newResult = ex.Message;
    }
    using var cleanup = newResult;

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        ProviderUtil.SetStateAndNotify(ref _filteredTableHandle, newResult, _observers);
      }
    }
  }
}
