using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableProvider :
  IObserver<RefCounted<StatusOr<TableHandle>>>,
  // IObservable<StatusOrView<TableHandle>>,  // redundant, part of ITableProvider
  ITableProvider {
  private const string UnsetTableHandleText = "[No Filtered Table]";


  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _syncRoot = new();
  private Action? _onDispose;
  private IDisposable? _tableHandleSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOrCounted<TableHandle> _filteredTableHandle =
    StatusOrCounted<TableHandle>.OfStatus(UnsetTableHandleText);

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

  public IDisposable Subscribe(IObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    // Locked because I want these to happen together.
    lock (_syncRoot) {
      _observers.Add(observer, out _);
      _observers.ZamboniOneNext(observer, _filteredTableHandle.Share());
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOrCounted<TableHandle>> observer) {
    _observers.Remove(observer, out var isLast);
    if (!isLast) {
      return;
    }
    lock (_syncRoot) {
      ZZTop.ClearAndDisposeInBackground(ref _tableHandleSubscriptionDisposer);
      ZZTop.ClearAndDisposeInBackground(ref _onDispose);
    }
    DisposeTableHandleState("Disposing FilteredTable");
  }

  private void DisposeTableHandleState(string statusMessage) {
    lock (_syncRoot) {
      StatusOrCounted.ResetWithStatus(ref _filteredTableHandle, statusMessage);
      _observers.Send(_filteredTableHandle.Share());
    }
  }

  public void OnNext(StatusOrCounted<TableHandle> parentHandle) {
    using var cleanup = parentHandle;
    if (!parentHandle.GetValueOrStatus(out _, out var status)) {
      DisposeTableHandleState(status);
      return;
    }

    DisposeTableHandleState("Filtering");
    var versionCookie = _versionParty.Mark();
    Utility.RunInBackground(() => OnNextBackground(versionCookie, parentHandle.Share()));
  }

  private void OnNextBackground(object versionCookie,
    StatusOrCounted<TableHandle> parentHandle) {
    using var cleanup1 = parentHandle;
    StatusOrCounted<TableHandle> result;
    try {
      var filtered = parentHandle.Value.Where(_condition);
      // This keeps the dependencies (parentHandle) alive as well.
      result = StatusOrCounted<TableHandle>.OfValue(filtered, parentHandle.Share());
    } catch (Exception ex) {
      result = StatusOrCounted<TableHandle>.OfStatus(ex.Message);
    }
    using var cleanup2 = result;

    versionCookie.Finish(() => {
      StatusOrCounted.Replace(ref _filteredTableHandle, result.Share());
      _observers.Enqueue(result.Share());
    });
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
