using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableProvider :
  IObserver<StatusOr<View<TableHandle>>>,
  // IObservable<StatusOrView<TableHandle>>,  // redundant, part of ITableProvider
  ITableProvider {
  private const string UnsetTableHandleText = "[No Filtered Table]";


  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _syncRoot = new();
  private IDisposable? _onDispose;
  private IDisposable? _tableHandleSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<View<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _filteredTableHandle =
    StatusOr<RefCounted<TableHandle>>.OfStatus(UnsetTableHandleText);
  private readonly KeepLatestExecutor _keepLatestExecutor = new();

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

  public void Start() {
    // My parent is a condition-free table that I subscribe to and filter with
    // my condition.
    var tq = new TableQuad(_endpointId, _persistentQueryId, _tableName, "");
    Debug.WriteLine($"FilteredTableProvider is subscribing to TableHandle with {tq}");
    lock (_syncRoot) {
      _tableHandleSubscriptionDisposer = _stateManager.SubscribeToTable(tq, this);
    }
  }

  public IDisposable Subscribe(IObserver<StatusOr<View<TableHandle>>> observer) {
    lock (_syncRoot) {
      _observers.AddAndNotify(observer, _filteredTableHandle.View(),
        _filteredTableHandle.Share());
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<View<TableHandle>>> observer) {
    lock (_syncRoot) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }
      Background.ClearAndDispose(ref _tableHandleSubscriptionDisposer);
      Background.ClearAndDispose(ref _onDispose);
    }
    ResetTableHandleStateAndNotify("Disposing FilteredTable");
  }

  private void ResetTableHandleStateAndNotify(string statusMessage) {
    lock (_syncRoot) {
      StatusOrCounted.ResetWithStatus(ref _filteredTableHandle, statusMessage);
      _observers.OnNext(_filteredTableHandle.View(), _filteredTableHandle.Share());
    }
  }

  public void OnNext(StatusOr<View<TableHandle>> parentHandle) {
    if (!parentHandle.GetValueOrStatus(out var th, out var status)) {
      ResetTableHandleStateAndNotify(status);
      return;
    }

    ResetTableHandleStateAndNotify("Filtering");
    // Share here while still on this thread. (Sharing inside the lambda is too late).
    var sharedParent = th.Share();
    _keepLatestExecutor.Run(cookie => OnNextBackground(cookie, sharedParent));
  }

  private void OnNextBackground(object versionCookie,
    RefCounted<TableHandle> parentHandle) {
    using var cleanup1 = parentHandle;

    TableHandle? filtered = null;
    string? exceptionMessage = null;
    try {
      filtered = parentHandle.Value.Where(_condition);
    } catch (Exception ex) {
      exceptionMessage = ex.Message;
    }

    versionCookie.Finish(() => {
      if (filtered != null) {
        ZZTop.Acquire(ref _filteredTableHandle, filtered, parentHandle.Share());
      } else {
        ZZTop.SetStatus(exceptionMessage);
      }
      _observers.Enqueue(ZZTop.AsView(_filteredTableHandle));
    });
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}

public static class StatusOrCounted {
  public static void ResetWithStatus<T>(ref StatusOr<RefCounted<T>> item, string statusMessage)
     where T : class, IDisposable {
    var (_, value) = item.Destructure();
    item = StatusOr<RefCounted<T>>.OfStatus(statusMessage);
    Background.ClearAndDispose(ref value);
  }
}
