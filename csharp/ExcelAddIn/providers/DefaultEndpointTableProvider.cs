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
  IStatusObserver<EndpointId>,
  IStatusObserverWithCookie<RefCounted<TableHandle>>,
  // IObservable<StatusOr<TableHandle>>,
  // IDisposable,
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Default Connection]";

  private readonly StateManager _stateManager;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private readonly Latch _subscribeDone = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _endpointSubscriptionDisposer = null;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly object _expectedCookie = new();
  private readonly ObserverContainer<RefCounted<TableHandle>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _tableHandle = UnsetTableHandleText;

  public DefaultEndpointTableProvider(StateManager stateManager,
    PqName? pqName, string tableName, string condition) {
    _stateManager = stateManager;
    _pqName = pqName;
    _tableName = tableName;
    _condition = condition;
  }

  public IDisposable Subscribe(IStatusObserver<RefCounted<TableHandle>> observer) {
    lock (_sync) {
      SorUtil.AddObserverAndNotify(_observers, observer, _tableHandle, out _);
      if (_subscribeDone.TrySet()) {
        _endpointSubscriptionDisposer = _stateManager.SubscribeToDefaultEndpoint(this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IStatusObserver<RefCounted<TableHandle>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _endpointSubscriptionDisposer);
      Utility.ClearAndDispose(ref _upstreamSubscriptionDisposer);
      SorUtil.Replace(ref _tableHandle, UnsetTableHandleText);
    }
  }

  public void OnNext(EndpointId? endpointId) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      // Unsubscribe from old upstream
      Utility.ClearAndDispose(ref _upstreamSubscriptionDisposer);
      // Suppress any notifications from the old subscription, which will now be stale
      _expectedCookie = new();

      // If endpoint is null, then don't resubscribe to anything.
      if (endpointId == null) {
        SorUtil.ReplaceAndNotify(ref _tableHandle, UnsetTableHandleText, _observers);
        return;
      }

      // Subscribe to a new upstream
      var tq = new TableQuad(endpointId, _pqName, _tableName, _condition);
      var observer = new ObserverWithCookie<StatusOr<TableHandle>>(this, _expectedCookie);
      _upstreamSubscriptionDisposer = _stateManager.SubscribeToTable(tq, observer);
    }
  }

  public void OnNext(RefCounted<TableHandle> value, object cookie) {
    lock (_sync) {
      if (_isDisposed.Value || !ReferenceEquals(_expectedCookie, cookie)) {
        return;
      }
      SorUtil.ReplaceAndNotify(ref _tableHandle, value, _observers);
    }
  }
}
