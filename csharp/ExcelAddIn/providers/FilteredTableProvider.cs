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
    Background.Run(Start);
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
      Background.Dispose(Utility.Exchange(ref _upstreamDisposer, null));
      Background.Dispose(Utility.Exchange(ref _onDispose, null));
    }
    ResetTableHandleStateAndNotify("Disposing FilteredTable");
  }

  private void ResetTableHandleStateAndNotify(string statusMessage) {
    lock (_sync) {
      Background.Dispose(_filteredTableHandle);
      _filteredTableHandle = KeepAlive.Register(StatusOr<TableHandle>.OfStatus(statusMessage));
      _observers.OnNext(_filteredTableHandle);
    }
  }

  public void OnNext(StatusOr<TableHandle> parentHandle) {

    var keptParent = KeepAlive.Reference(parentHandle);
    ResetTableHandleStateAndNotify("Filtering");
    // Share here while still on this thread. (Sharing inside the lambda is too late).
    lock (_sync) {
      // Need these two values created in this thread (not in the body of the lambda).
      var cookie = new object();
      _latestCookie = cookie;
      Background.Run(() => OnNextBackground(keptParent, cookie));
    }
  }

  private void OnNextBackground(KeptAlive<StatusOr<TableHandle>> keptParent, object versionCookie) {
    StatusOr<TableHandle> newFiltered;
    if (!keptParent.Target.GetValueOrStatus(out var th, out var status)) {
      newFiltered = StatusOr<TableHandle>.OfStatus(status);
    } else {
      try {
        // This is a server call that may take some time.
        var childHandle = th.Where(_condition);
        newFiltered = StatusOr<TableHandle>.OfValue(childHandle);
      } catch (Exception ex) {
        newFiltered = StatusOr<TableHandle>.OfStatus(ex.Message);
      }
    }
    var newKept = KeepAlive.Register(newFiltered, keptParent);

    lock (_sync) {
      if (!Object.ReferenceEquals(versionCookie, _latestCookie)) {
        return;
      }

      Background.Dispose(_filteredTableHandle);
      _filteredTableHandle = newKept;
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

