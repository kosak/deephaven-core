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
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _tableHandle);
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
      _observers.RemoveAndWait(observer, out var isLast);
      if (!isLast) {
        return;
      }
      // Do this teardowns synchronously.
      Utility.Exchange(ref _upstreamDisposer, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(_tableHandle.Move());
    }
  }

  public void OnNext(StatusOr<Client> client) {
    lock (_sync) {
      if (!client.GetValueOrStatus(out _, out var status)) {
        Utility.SetStateAndNotify(_observers, ref _tableHandle, status);
        return;
      }

      Utility.SetStateAndNotify(_observers, ref _tableHandle, "Fetching Table");
      // These two values need to be created early (not on the lambda, which is on a different thread)
      var clientCopy = client.Copy();
      var cookie = _versionTracker.SetNewVersion();
      Background666.Run(() => OnNextBackground(clientCopy.Move(), cookie));
    }
  }

  private void OnNextBackground(StatusOr<Client> clientCopy,
    VersionTrackerCookie cookie) {
    using var client = clientCopy;

    StatusOr<TableHandle> newState;
    try {
      var (cli, _) = client;
      var th = cli.Manager.FetchTable(_tableName);
      // Keep a dependency on client
      newState = StatusOr<TableHandle>.OfValue(th, client);
    } catch (Exception ex) {
      newState = ex.Message;
    }
    using var cleanup = newState;

    lock (_sync) {
      if (cookie.IsCurrent) {
        _observers.SetStateAndNotify(ref _tableHandle, newState);
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
