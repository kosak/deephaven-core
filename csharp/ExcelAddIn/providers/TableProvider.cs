using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableProvider :
  IObserver<StatusOr<Client>>,
  IObservable<StatusOr<TableHandle>> {
  private const string UnsetTableHandleText = "[No Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly object _sync = new();
  private IDisposable? _onDispose;
  private IDisposable? _upstreamDisposer = null;
  private readonly SequentialExecutor _executor = new();
  private readonly VersionTracker _versionTracker = new();
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers;
  private StatusOr<TableHandle> _tableHandle = UnsetTableHandleText;

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, IDisposable? onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _onDispose = onDispose;
    _observers = new(_executor);
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _tableHandle);
      if (isFirst) {
        // Subscribe to parents at the time of the first subscription.
        _upstreamDisposer = _persistentQueryId != null
          ? _stateManager.SubscribeToPersistentQuery(_endpointId, _persistentQueryId, this)
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
      // Do these teardowns synchronously.
      Utility.Exchange(ref _upstreamDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(_tableHandle.Move());
    }
  }

  public void OnNext(StatusOr<Client> client) {
    lock (_sync) {
      if (!client.GetValueOrStatus(out _, out var status)) {
        SetStateAndNotifyLocked(status);
        return;
      }

      SetStateAndNotifyLocked("Fetching Table");
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
      newState = StatusOr<TableHandle>.OfStatus(ex.Message);
    }
    using var cleanup = newState;

    lock (_sync) {
      if (cookie.IsCurrent) {
        SetStateAndNotifyLocked(newState);
      }
    }
  }

  private void SetStateAndNotifyLocked(StatusOr<TableHandle> newState) {
    Background666.InvokeDispose(_tableHandle);
    _tableHandle = newState.Copy();
    _observers.OnNext(newState);
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
