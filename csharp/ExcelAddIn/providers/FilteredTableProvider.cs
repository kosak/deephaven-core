using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableProvider :
  IObserver<StatusOr<TableHandle>>,
  IObservable<StatusOr<TableHandle>> {
  private const string UnsetTableHandleText = "[No Filtered Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private IDisposable? _onDispose;
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<TableHandle> _filteredTableHandle = UnsetTableHandleText;

  public FilteredTableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, string condition,
    IDisposable? onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _condition = condition;
    _onDispose = onDispose;
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _filteredTableHandle);
      if (isFirst) {
        // Subscribe to parents at the time of the first subscription.
        var tq = new TableQuad(_endpointId, _persistentQueryId, _tableName, "");
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
      // Do these teardowns synchronously.
      Utility.Exchange(ref _upstreamDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(_filteredTableHandle.Move());
    }
  }

  public void OnNext(StatusOr<TableHandle> parentHandle) {
    lock (_sync) {
      if (!parentHandle.GetValueOrStatus(out _, out var status)) {
        SetStateAndNotifyLocked(status);
        return;
      }
      SetStateAndNotifyLocked("Filtering");
      // These two values need to be created early (not on the lambda, which is on a different thread)
      var parentHandleCopy = parentHandle.Copy();
      var cookie = _versionTracker.SetNewVersion();
      Background666.Run(() => OnNextBackground(parentHandleCopy.Move(), cookie));
    }
  }

  private void OnNextBackground(StatusOr<TableHandle> parentHandleCopy,
    VersionTrackerCookie versionCookie) {
    using var parentHandle = parentHandleCopy;
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
        SetStateAndNotifyLocked(newResult);
      }
    }
  }

  private void SetStateAndNotifyLocked(StatusOr<TableHandle> newState) {
    Background666.InvokeDispose(_filteredTableHandle);
    _filteredTableHandle = newState;
    _observers.OnNext(newState);
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
