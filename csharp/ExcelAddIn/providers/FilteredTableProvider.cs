using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableProvider :
  IObserver<View<StatusOr<TableHandle>>>,
  // IObservable<StatusOrView<TableHandle>>,  // redundant, part of ITableProvider
  ITableProvider {

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _syncRoot = new();
  private Action? _onDispose;
  private IDisposable? _tableHandleSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private RefCounted<StatusOr<TableHandle>> _filteredTableHandle = StatusOrRef<TableHandle>.OfStatus("[No Filtered Table]");

  public FilteredTableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, string condition,
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

  public IDisposable Subscribe(IObserver<View<StatusOr<TableHandle>>> observer) {
    // Locked because I want these to happen together.
    lock (_syncRoot) {
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
    lock (_syncRoot) {
      disp1 = Utility.Release(ref _tableHandleSubscriptionDisposer);
      disp2 = Utility.Release(ref _onDispose);
    }

    Utility.DisposeInBackground(disp1);
    Utility.DisposeInBackground(disp2);
    DisposeTableHandleState("Disposing FilteredTable");
  }

  private void DisposeTableHandleState(string statusMessage) {
    var state = RefCounted.Acquire(StatusOr<TableHandle>.OfStatus(statusMessage));
    lock (_syncRoot) {
      Utility.Swap(ref _filteredTableHandle, ref state);
    }
    Utility.DisposeInBackground(state);
  }

  public void OnNext(View<StatusOr<TableHandle>> parentHandle) {
    DisposeTableHandleState("Filtering");
    var versionCookie = new object();
    lock (_syncRoot) {
      _latestCookie.SetValue(versionCookie);
    }
    var sharedPh = parentHandle.Share();
    Utility.RunInBackground(() => OnNextBackground(versionCookie, sharedPh));
  }

  private void OnNextBackground(object versionCookie,
    RefCounted<StatusOr<TableHandle>> tableHandle) {
    StatusOr<TableHandle> result;
    if (!tableHandle.Value.GetValueOrStatus(out var th, out var status)) {
      result = StatusOr<TableHandle>.OfStatus(status);
    } else {
      try {
        var filtered = th.Where(_condition);
        result = StatusOr<TableHandle>.OfValue(filtered);
      } catch (Exception ex) {
        result = StatusOr<TableHandle>.OfStatus(ex.Message);
      }
    }

    var state = RefCounted.Acquire(result);
    lock (_syncRoot) {
      if (object.ReferenceEquals(versionCookie, _latestCookie)) {
        Utility.Swap(ref _filteredTableHandle, ref state);
        _observers.Enqueue(_filteredTableHandle.View);
      }
    }
    state.Dispose();
    tableHandle.Dispose();
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
