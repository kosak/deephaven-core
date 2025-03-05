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
  IObserver<EndpointId?>,
  IObserverWithCookie<StatusOr<TableHandle>>,
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
  private readonly VersionTracker _versionTracker = new();
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOr<TableHandle> _tableHandle = UnsetTableHandleText;

  public DefaultEndpointTableProvider(StateManager stateManager,
    PqName? pqName, string tableName, string condition) {
    _stateManager = stateManager;
    _pqName = pqName;
    _tableName = tableName;
    _condition = condition;
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _tableHandle, out _);
      if (_subscribeDone.Set()) {
        _endpointSubscriptionDisposer = _stateManager.SubscribeToDefaultEndpointSelection(this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.Set()) {
        return;
      }
      Utility.ClearAndDispose(ref _endpointSubscriptionDisposer);
      Utility.ClearAndDispose(ref _upstreamSubscriptionDisposer);
      ProviderUtil.SetState(ref _tableHandle, UnsetTableHandleText);
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
      var cookie = _versionTracker.New();

      // If endpoint is null, then don't resubscribe to anything.
      if (endpointId == null) {
        ProviderUtil.SetStateAndNotify(ref _tableHandle, UnsetTableHandleText, _observers);
        return;
      }

      // Subscribe to a new upstream
      var tq = new TableQuad(endpointId, _pqName, _tableName, _condition);
      var observer = new ObserverWithCookie<StatusOr<TableHandle>>(this, cookie);
      _upstreamSubscriptionDisposer = _stateManager.SubscribeToTable(tq, observer);
    }
  }

  public void OnNextWithCookie(StatusOr<TableHandle> value, VersionTracker.Cookie cookie) {
    lock (_sync) {
      if (_isDisposed.Value || !cookie.IsCurrent) {
        return;
      }
      ProviderUtil.SetStateAndNotify(ref _tableHandle, value, _observers);
    }
  }

  public void OnCompletedWithCookie(VersionTracker.Cookie cookie) {
    throw new NotImplementedException();
  }

  public void OnErrorWithCookie(Exception ex, VersionTracker.Cookie cookie) {
    throw new NotImplementedException();
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
