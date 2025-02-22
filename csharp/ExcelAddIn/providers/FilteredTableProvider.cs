using System.CodeDom.Compiler;
using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
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
  private KeptAlive<StatusOr<TableHandle>> _filteredTableHandle;
  private object _latestCookie = new();

  public FilteredTableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, string condition,
    IDisposable? onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _condition = condition;
    _onDispose = onDispose;
    _filteredTableHandle = KeepAlive.Register(StatusOr<TableHandle>.OfStatus(UnsetTableHandleText));

    // Do my subscriptions on a separate thread to avoid reentrancy on StateManager
    _executor.Run(Start);
  }

  private void Start() {
    // My parent is a condition-free table that I observe. I provide my observers
    // with that table filtered by a condition.
    var tq = new TableQuad(_endpointId, _persistentQueryId, _tableName, "");
    Debug.WriteLine($"FilteredTableProvider is subscribing to TableHandle with {tq}");
    var temp = _stateManager.SubscribeToTable(tq, this);

    lock (_sync) {
      _upstreamDisposer = temp;
    }
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Add(observer, out _);
      _observers.OnNextOne(observer, _filteredTableHandle);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }
      _executor.Dispose(Utility.Exchange(ref _upstreamDisposer, null));
      _executor.Dispose(Utility.Exchange(ref _onDispose, null));
    }
    ResetTableHandleStateAndNotify("Disposing FilteredTable");
  }

  private void ResetTableHandleStateAndNotify(string statusMessage) {
    lock (_sync) {
      _executor.Dispose(_filteredTableHandle);
      _filteredTableHandle = KeepAlive.Register(StatusOr<TableHandle>.OfStatus(statusMessage));
      _observers.OnNext(_filteredTableHandle);
    }
  }

  public void OnNext(StatusOr<TableHandle> parentHandle) {
    if (!parentHandle.GetValueOrStatus(out _, out var status)) {
      SetStateAndNotify(status);
      return;
    }

    SetStateAndNotify("Filtering");
    var keptParentHandle = KeepAlive.Reference(parentHandle);
    var cookie = new object();
    lock (_sync) {
      _latestCookie = cookie;
      // These two values need to be created early (not on the lambda, which is on a different thread)
      Background.Run(() => OnNextBackground(keptParentHandle, cookie));
    }
  }

  private void OnNextBackground(KeptAlive<StatusOr<TableHandle>> keptParent, object versionCookie) {
    using var cleanup1 = keptParent;
    StatusOr<TableHandle> newFiltered;
    try {
      // This is a server call that may take some time.
      var childHandle = th.Where(_condition);
      newFiltered = StatusOr<TableHandle>.OfValue(childHandle);
    } catch (Exception ex) {
      newFiltered = StatusOr<TableHandle>.OfStatus(ex.Message);
    }
    using var newTh = KeepAlive.Register(newFiltered, keptParent.Target);

    lock (_sync) {
      if (!Object.ReferenceEquals(versionCookie, _latestCookie)) {
        return;
      }

      Background.Dispose(_filteredTableHandle);
      _filteredTableHandle = KeepAlive.Register(newFiltered, keptParent.Target);
      _observers.OnNext(_filteredTableHandle);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}

