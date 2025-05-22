using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/**
 * The job of this class is to observe notifications for the currently specified default EndpointId,
 * if any, and then upon receiving such a notification, subscribe to the table provider for the key
 * (endpoint, pqName, tableName, condition). Then, as that table provider provides me with
 * TableHandles or status messages, forward those to my observers.
 */
internal class DefaultEndpointTableProvider :
  IValueObserverWithCancel<StatusOr<EndpointId>>,
  IValueObserverWithCancel<StatusOr<RefCounted<TableHandle>>>,
  // IValueObservable<StatusOr<RefCounted<TableHandle>>>
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Default Connection]";

  private readonly StateManager _stateManager;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private CancellationTokenSource _endpointTokenSource = new();
  private CancellationTokenSource _tableHandleTokenSource = new();
  private IDisposable? _endpointSubscriptionDisposer = null;
  private IDisposable? _tableHandleSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _tableHandle = UnsetTableHandleText;

  public DefaultEndpointTableProvider(StateManager stateManager,
    PqName? pqName, string tableName, string condition) {
    _stateManager = stateManager;
    _pqName = pqName;
    _tableName = tableName;
    _condition = condition;
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _tableHandle, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create<StatusOr<EndpointId>>(
          this, _endpointTokenSource.Token);
        _endpointSubscriptionDisposer = _stateManager.SubscribeToDefaultEndpoint(voc);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _endpointTokenSource.Cancel();
      _endpointTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _endpointSubscriptionDisposer);
      Utility.ClearAndDispose(ref _tableHandleSubscriptionDisposer);
      StatusOrUtil.Replace(ref _tableHandle, UnsetTableHandleText);
    }
  }

  public void OnNext(StatusOr<EndpointId> endpointId, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Invalidate any inflight TableHandle notifications
      _tableHandleTokenSource.Cancel();
      _tableHandleTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
        _endpointTokenSource.Token);

      // Unsubscribe from old upstream TableHandle
      Utility.ClearAndDispose(ref _tableHandleSubscriptionDisposer);
      // Suppress any notifications from the old subscription, which will now be stale

      if (!endpointId.GetValueOrStatus(out var ep, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _tableHandle, status, _observers);
        return;
      }

      // Subscribe to a new upstream TableProvide
      var tq = new TableQuad(ep, _pqName, _tableName, _condition);
      var voc = ValueObserverWithCancelWrapper.Create<StatusOr<RefCounted<TableHandle>>>(
        this, _tableHandleTokenSource.Token);
      _tableHandleSubscriptionDisposer = _stateManager.SubscribeToTable(tq, voc);
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> value,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _tableHandle, value, _observers);
    }
  }
}
