using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class DefaultEndpointTableProvider :
  IObserver<StatusOr<TableHandle>>,
  IObserver<EndpointId?>,
  IObservable<StatusOr<TableHandle>> {
  private const string UnsetTableHandleText = "[No Default Connection]";

  private readonly StateManager _stateManager;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private IDisposable? _onDispose;
  private IDisposable? _endpointSubscriptionDisposer = null;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly SequentialExecutor _executor = new();
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers;
  private StatusOr<TableHandle> _tableHandle = UnsetTableHandleText;

  public DefaultEndpointTableProvider(StateManager stateManager,
    PersistentQueryId? persistentQueryId, string tableName, string condition,
    IDisposable? onDispose) {
    _stateManager = stateManager;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _condition = condition;
    _onDispose = onDispose;
    _observers = new(_executor);
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _tableHandle);
      if (isFirst) {
        _endpointSubscriptionDisposer = _stateManager.SubscribeToDefaultEndpointSelection(this);
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
      Utility.Exchange(ref _endpointSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(_tableHandle.Move());
    }
  }

  public void OnNext(EndpointId? endpointId) {
    lock (_sync) {
      // Unsubscribe from old upstream
      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();

      // If endpoint is null, then don't resubscribe to anything.
      if (endpointId == null) {
        SetStateAndNotifyLocked(MakeState(UnsetTableHandleText));
        return;
      }

      // Subscribe to a new upstream
      var tq = new TableQuad(endpointId, _persistentQueryId, _tableName, _condition);
      _upstreamSubscriptionDisposer = _stateManager.SubscribeToTable(tq, this);
    }
  }

  public void OnNext(StatusOr<TableHandle> value) {
    lock (_sync) {
      SetStateAndNotifyLocked(value);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
