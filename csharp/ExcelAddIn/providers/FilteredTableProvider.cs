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
    lock (_syncRoot) {
      var cookie = new object();
      _lastCookie = cookie;
      // We want cookie here, not _lastCookie. Cooke is effectively constant here,
      // but _lastCookie can change by some other thread before the lambda is run.
      Background.Run(() => OnNextBackground(cookie, sharedParent));
    }
  }

  private void OnNextBackground(object versionCookie,
    RefCounted<TableHandle> parentHandle) {
    using var cleanup1 = parentHandle;

    RefCounted<TableHandle>? filteredRc = null;
    string? exceptionMessage = null;
    try {
      var filtered = parentHandle.Value.Where(_condition);
      // parentHandle is the dependency.
      filteredRc = RefCounted.Acquire(filtered, parentHandle.Share());
    } catch (Exception ex) {
      exceptionMessage = ex.Message ?? "";
    }
    using var cleanup2 = filteredRc;

    lock (_syncRoot) {
      if (!Object.ReferenceEquals(versionCookie, _latestCookie)) {
        return;
      }
      if (filteredRc != null) {
        StatusOrCounted.SetValue(ref _filteredTableHandle, filteredRc.View());
      } else {
        StatusOrCounted.SetStatus(ref _filteredTableHandle, exceptionMessage!);
      }
      _observers.OnNext(_filteredTableHandle.View(), _filteredTableHandle.Share());
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}

public static class StatusOrCounted {
  public static void SetStatus<T>(ref StatusOr<RefCounted<T>> target, string statusMessage)
     where T : class, IDisposable {
    var (_, rct) = target.Destructure();
    target = StatusOr<RefCounted<T>>.OfStatus(statusMessage);
    Background.ClearAndDispose(ref rct);
  }

  public static void SetValue<T>(ref StatusOr<RefCounted<T>> target, View<T> value)
    where T : class, IDisposable {
    var (_, rct) = target.Destructure();
    target = StatusOr<RefCounted<T>>.OfValue(value.Share());
    Background.ClearAndDispose(ref rct);
  }
}
