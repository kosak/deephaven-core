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
  IValueObserver<StatusOr<EndpointId>>,
  IValueObserver<StatusOr<RefCounted<TableHandle>>>,
  // IValueObserver<StatusOr<TableHandle>>,
  // IDisposable,
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Default Connection]";

  private readonly StateManager _stateManager;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private readonly FreshnessSource _freshness;
  private readonly Latch _subscribeDone = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _endpointSubscriptionDisposer = null;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _tableHandle = UnsetTableHandleText;

  public DefaultEndpointTableProvider(StateManager stateManager,
    PqName? pqName, string tableName, string condition) {
    _stateManager = stateManager;
    _pqName = pqName;
    _tableName = tableName;
    _condition = condition;
    _freshness = new(_sync);
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _tableHandle, out _);
      if (_subscribeDone.TrySet()) {
        _endpointSubscriptionDisposer = _stateManager.SubscribeToDefaultEndpoint(this);
      }
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _endpointSubscriptionDisposer);
      Utility.ClearAndDispose(ref _upstreamSubscriptionDisposer);
      StatusOrUtil.Replace(ref _tableHandle, UnsetTableHandleText);
    }
  }

  public void OnNext(StatusOr<EndpointId> endpointId) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      // Unsubscribe from old upstream
      Utility.ClearAndDispose(ref _upstreamSubscriptionDisposer);
      // Suppress any notifications from the old subscription, which will now be stale
      var token = _freshness.Refresh();

      if (!endpointId.GetValueOrStatus(out var ep, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _tableHandle, UnsetTableHandleText, _observers);
        return;
      }

      // Subscribe to a new upstream
      var tq = new TableQuad(ep, _pqName, _tableName, _condition);
      var fobs = new ValueObserverFreshnessFilter<StatusOr<RefCounted<TableHandle>>>(
        this, token);
      _upstreamSubscriptionDisposer = _stateManager.SubscribeToTable(tq, fobs);
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> value) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _tableHandle, value, _observers);
    }
  }
}
