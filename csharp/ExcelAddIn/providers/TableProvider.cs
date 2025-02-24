using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/**
 * This class has two functions, depending on whether the persistentQueryId is set.
 * If it is set, the class assumes that this is an Enterprise Core Plus table, and
 * follows that workflow.
 *
 * Otherwise, if it is not set, the class assumes that this is a Community Core table,
 * and follows that workflow.
 *
 * The job of this class is to subscribe to a PersistentQueryMapper with the key
 * (endpoint, pqName). Then, as that PQ mapper provider provides me with PqIds
 * with TableHandles (or status messages), process them.
 *
 * If the message received was a status message, then forward it to my observers.
 * If it was a PqId, then 
 *
 * If it was a TableHandle, then filter it by "condition" in the background, and provide
 * the resulting filtered TableHandle (or error) to my observers.
 */

internal class TableProvider :
  IObserver<StatusOr<Client>>,
  IObservable<StatusOr<TableHandle>> {
  private const string UnsetTableHandleText = "[No Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryName? _pqName;
  private readonly string _tableName;
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private IDisposable? _upstreamDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOr<TableHandle> _tableHandle = UnsetTableHandleText;

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryName? pqName, string tableName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _tableHandle, out var isFirst);
      if (isFirst) {
        // Subscribe to parents at the time of the first subscription.
        _upstreamDisposer = _pqName != null
          ? _stateManager.SubscribeToCorePlusClient(_endpointId, _pqName, this)
          : _stateManager.SubscribeToCoreClient(_endpointId, this);
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
      _isDisposed = true;
      Utility.ClearAndDispose(ref _upstreamDisposer);
      ProviderUtil.SetState(ref _tableHandle, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<Client> client) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      // Suppress responses from stale background workers.
      var cookie = _versionTracker.New();
      if (!client.GetValueOrStatus(out _, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _tableHandle, status, _observers);
        return;
      }

      ProviderUtil.SetStateAndNotify(ref _tableHandle, "Fetching Table", _observers);
      var clientCopy = client.Share();
      Background666.Run(() => OnNextBackground(clientCopy, cookie));
    }
  }

  private void OnNextBackground(StatusOr<Client> clientCopy, VersionTracker.Cookie cookie) {
    using var cleanup1 = clientCopy;

    StatusOr<TableHandle> newState;
    try {
      var (cli, _) = clientCopy;
      var th = cli.Manager.FetchTable(_tableName);
      // Keep a dependency on client
      newState = StatusOr<TableHandle>.OfValue(th, clientCopy);
    } catch (Exception ex) {
      newState = ex.Message;
    }
    using var cleanup2 = newState;

    lock (_sync) {
      if (cookie.IsCurrent) {
        ProviderUtil.SetStateAndNotify(ref _tableHandle, newState, _observers);
      }
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
