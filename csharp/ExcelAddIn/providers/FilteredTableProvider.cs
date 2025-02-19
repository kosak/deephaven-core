using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableProvider :
  IObserver<StatusOr<View<TableHandle>>>,
  // IObservable<StatusOr<View<TableHandle>>>,  // redundant, part of ITableProvider
  ITableProvider {

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly string _condition;
  private Action? _onDispose;
  private readonly SequentialExecutor _executor = new();
  private IDisposable? _tableHandleSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOrRef<TableHandle> _filteredTableHandle = StatusOrRef<TableHandle>.OfStatus("[No Filtered Table]");

  public FilteredTableProvider(StateManager stateManager,
    EndpointId endpointId, PersistentQueryId? persistentQueryId, string tableName, string condition,
    Action onDispose) {
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
    Debug.WriteLine($"FTP is subscribing to TableHandle with {tq}");
    _tableHandleSubscriptionDisposer = _stateManager.SubscribeToTable(tq, this);
  }

  public IDisposable Subscribe(IObserver<StatusOrView<TableHandle>> observer) {
    // Locked because I want these to happen together.
    lock (_sync) {
      _observers.Add(observer, out _);
      _observers.ZamboniOneNext(observer, _filteredTableHandle);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver() {
    _observers.Remove(observer, out var isLast);
    if (!isLast) {
      return;
    }
    IDisposable disp1;
    IDisposable disp2;
    lock (_sync) {
      disp1 = Utility.Release(ref _tableHandleSubscriptionDisposer);
      disp2 = Utility.Release(ref _onDispose);
    }

    Utility.DisposeInBackground(disp1);
    Utility.DisposeInBackground(disp2);
    DisposeTableHandleState("Disposing FilteredTable");
  }

  private void DisposeTableHandleState(string statusMessage) {
    var newState = StatusOrRef<TableHandle>.OfStatus(statusMessage);
    lock (_sync) {
      var oldState = Utility.Swap(ref _filteredTableHandle, newState);
    }
    Utility.DisposeInBackground(oldState);
  }

  public void OnNext(StatusOrView<TableHandle> parentHandle) {
    DisposeTableHandleState("Filtering");
    var versionCookie = new object();
    lock (_sync) {
      _latestCookie.SetValue(versionCookie);
    }
    Utility.RunInBackground(() => OnNextBackground(versionCookie, parentHandle.Share()));
  }

  private void OnNextBackground(object versionCookie, StatusOrRef<TableHandle> tableHandle) {
    // Dispose on the way out
    using var th = tableHandle;
    // Dispose on the way out. Make a copy with Share() if you need to save it.
    using var result = OnNextBackgroundHelper(th.View);
    lock (_sync) {
      if (!object.ReferenceEquals(versionCookie, _latestCookie)) {
        // Stale, do nothing
        return;
      }

      _filteredTableHandle = result.Share();
      _observers.Enqueue(_filteredTableHandle.View);
    }
  }

  private StatusOrRef<TableHandle> OnNextBackgroundHelper(
    StatusOrView<TableHandle> parent) {
    try {
      if (!parent.GetValueOrStatus(out var th, out var status)) {
        return StatusOrRef<TableHandle>.OfStatus(status);
      }

      var filtered = th.Where(_condition);
      return StausOrRef<TableHandle>.OfRef(filtered);
    } catch (Exception ex) {
      return StausOrRef<TableHandle>.OfStatus(ex.Message);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
