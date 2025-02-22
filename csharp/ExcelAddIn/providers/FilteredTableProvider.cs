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
  private readonly SequentialExecutor _executor = new();
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers;
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
    _observers = new(_executor);
    _filteredTableHandle = KeepAlive.Register(StatusOr<TableHandle>.OfStatus(UnsetTableHandleText));
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _filteredTableHandle.Target);
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
        SetStateAndNotifyLocked(MakeState(status));
        return;
      }
      SetStateAndNotifyLocked(MakeState("Filtering"));
      var keptParentHandle = KeepAlive.Reference(parentHandle);
      var cookie = new object();
      _latestCookie = cookie;
      // These two values need to be created early (not on the lambda, which is on a different thread)
      _executor.Run(() => OnNextBackground(keptParentHandle.Move(), cookie));
    }
  }

  private void OnNextBackground(KeptAlive<StatusOr<TableHandle>> keptTableHandle, object versionCookie) {
    using var cleanup1 = keptTableHandle;
    StatusOr<TableHandle> newResult;
    try {
      // This is a server call that may take some time.
      var (th, _) = keptTableHandle.Target;
      var childHandle = th.Where(_condition);
      newResult = StatusOr<TableHandle>.OfValue(childHandle);
    } catch (Exception ex) {
      newResult = StatusOr<TableHandle>.OfStatus(ex.Message);
    }
    using var newKeeper = KeepAlive.Register(newResult);

    lock (_sync) {
      if (!Object.ReferenceEquals(versionCookie, _latestCookie)) {
        return;
      }

      SetStateAndNotifyLocked(newKeeper.Move());
    }
  }

  private static KeptAlive<StatusOr<TableHandle>> MakeState(string status) {
    var state = StatusOr<TableHandle>.OfStatus(status);
    return KeepAlive.Register(state);
  }

  private void SetStateAndNotifyLocked(KeptAlive<StatusOr<TableHandle>> newState) {
    Background666.InvokeDispose(_filteredTableHandle);
    _filteredTableHandle = newState;
    _observers.OnNext(newState.Target);
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
