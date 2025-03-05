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
  IObserver<StatusOr<TableHandle>>,
  IObservable<StatusOr<TableHandle>> {
  private const string UnsetTableHandleText = "[No Filtered Table]";

  private readonly StateManager _stateManager;
  private readonly string _endpointId;
  private readonly string? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<TableHandle> _filteredTableHandle = UnsetTableHandleText;

  public FilteredTableProvider(StateManager stateManager, string endpointId,
    string? pqName, string tableName, string condition) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
    _condition = condition;
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _filteredTableHandle, out var isFirst);
      if (isFirst) {
        // Subscribe to parent at the time of the first subscription.
        var tq = new TableQuad(_endpointId, _pqName, _tableName, "");
        Debug.WriteLine($"FilteredTableProvider is subscribing to TableHandle with {tq}");
        _upstreamDisposer = _stateManager.SubscribeToTable(tq, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      _isDisposed = true;
      Utility.ClearAndDispose(ref _upstreamDisposer);
      ProviderUtil.SetState(ref _filteredTableHandle, "[Disposed");
    }
  }

  public void OnNext(StatusOr<TableHandle> parentHandle) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }

      // Invalidate any outstanding background work
      var cookie = _versionTracker.New();

      if (!parentHandle.GetValueOrStatus(out _, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _filteredTableHandle, status, _observers);
        return;
      }
      ProviderUtil.SetStateAndNotify(ref _filteredTableHandle, "Filtering", _observers);
      // This needs to be created early (not on the lambda, which is on a different thread)
      var parentHandleShare = parentHandle.Share();
      Background666.Run(() => OnNextBackground(parentHandleShare, cookie));
    }
  }

  private void OnNextBackground(StatusOr<TableHandle> parentHandleShare,
    VersionTracker.Cookie versionCookie) {
    using var parentHandle = parentHandleShare;
    StatusOr<TableHandle> newResult;
    try {
      // This is a server call that may take some time.
      var (th, _) = parentHandle;
      var childHandle = th.Where(_condition);
      newResult = StatusOr<TableHandle>.OfValue(childHandle);
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

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
