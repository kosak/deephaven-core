using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Auth;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

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
  private StatusOr<RefCounted<TableHandle>> _filteredTableHandle;

  public FilteredTableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, string condition,
    Action onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _condition = condition;
    _onDispose = onDispose;
    ZZTop.SetStatus(ref _filteredTableHandle, UnsetTableHandleText);
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
      _observers.ZamboniOneNext(observer, ZZTop.Share(_filteredTableHandle));
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<RefCounted<StatusOr<TableHandle>>> observer) {
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
      ZZTop.SetStatus(ref _filteredTableHandle, statusMessage);
    }
    _observers.Send(ZZTop.Share(_filteredTableHandle));
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> parentHandle) {
    if (!parentHandle.GetValueOrStatus(out var cli, out var status)) {
      DisposeTableHandleState(status);
      return;
    }

    DisposeTableHandleState("Filtering");
    var versionCookie = _versionParty.Mark();
    Utility.RunInBackground(() => OnNextBackground(versionCookie, cli));
  }

  private void OnNextBackground(object versionCookie,
    RefCounted<TableHandle> parentHandle) {
    using var cleanup1 = parentHandle;
    StatusOr<RefCounted<TableHandle>> result = null;
    try {
      var filtered = parentHandle.Value.Where(_condition);
      ZZTop.Acquire(ref result, filtered, parentHandle.Share());
    } catch (Exception ex) {
      ZZTop.SetStatus(ref result, ex.Message);
    }


  // The derived table handle has a sharing dependency on the parent
    // table handle, which in turn has a dependency on Client etc.
    var state = RefCounted.Acquire(result, parentHandle.Share());

    lock (_syncRoot) {
      if (object.ReferenceEquals(versionCookie, _latestCookie)) {
        Utility.Swap(ref _filteredTableHandle, ref state);
        _observers.Enqueue(_filteredTableHandle.Share());
      }
    }
    state.Dispose();
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
