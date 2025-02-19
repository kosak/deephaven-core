using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableProvider :
  IObserver<StatusOr<View<TableHandle>>>,
  IObservable<StatusOr<View<TableHandle>>> {

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly string _condition;
  private Action? _onDispose;
  private readonly SequentialExecutor _executor = new();
  private IDisposable? _tableHandleSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _filteredTableHandle = StatusOr<RefCounted<TableHandle>>.OfStatus("[No Filtered Table]");

  public FilteredTableProvider(StateManager stateManager,
    EndpointId endpointId, PersistentQueryId? persistentQueryId, string tableName, string condition,
    Action onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _condition = condition;
    _onDispose = onDispose;

    // Subscribe to a condition-free table
    var tq = new TableQuad(_endpointId, _persistentQueryId, _tableName, "");
    Debug.WriteLine($"FTP is subscribing to TableHandle with {tq}");
    _tableHandleSubscriptionDisposer = _stateManager.SubscribeToTable(tq, this);
  }

  public IDisposable Subscribe(IObserver<StatusOr<View<TableHandle>>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_filteredTableHandle);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _tableHandleSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeTableHandleState();
    });
  }

  public void OnNext(StatusOr<View<TableHandle>> tableHandleView) {
    var tableHandle = tableHandleView.Share();
    var versionCookie = new object();
    _latestCookie.SetValue(versionCookie);
    _executor.EnqueueIndependentWork(() => OnNextIndependentWork(versionCookie, tableHandle));
  }

  private void OnNextIndependentWork(object versionCookie, StatusOr<RefCounted<TableHandle>> tableHandle) {
    try {
      if (!tableHandle.GetValueOrStatus(out var th, out var status)) {
        OnNextStoreResult(versionCookie, result);
      } else {
        // TODO: send two messages
        OnNextStoreResult(versionCookie, result);
        _observers.SetAndSendStatus(ref _filteredTableHandle, "Filtering");

        try {
          var filtered = th.Where(_condition);
          _observers.SetAndSendValue(ref _filteredTableHandle, filtered);
          OnNextStoreResult(versionCookie, result);
        } catch (Exception ex) {
          _observers.SetAndSendStatus(ref _filteredTableHandle, ex.Message);
          OnNextStoreResult(versionCookie, result);
        }
      }
    } finally {
      tableHandle.Dispose();
    }
  }

  private void DisposeTableHandleState() {
    if (_workerThread.EnqueueOrNop(DisposeTableHandleState)) {
      return;
    }

    _ = _filteredTableHandle.GetValueOrStatus(out var oldTh, out _);
    _observers.SetAndSendStatus(ref _filteredTableHandle, "Disposing TableHandle");

    if (oldTh != null) {
      Utility.RunInBackground(oldTh.Dispose);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
