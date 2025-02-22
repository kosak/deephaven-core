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
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers;
  private KeptAlive<StatusOr<TableHandle>> _tableHandle;
  private object _latestCookie = new();

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, IDisposable? onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _onDispose = onDispose;
    _observers = new(_executor);
    _tableHandle = KeepAlive.Register(StatusOr<TableHandle>.OfStatus(UnsetTableHandleText));
  }

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _tableHandle.Target);
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
        SetStateAndNotifyLocked(MakeState(status));
        return;
      }

      SetStateAndNotifyLocked(MakeState("Fetching Table"));
      var keptClient = KeepAlive.Reference(client);
      var cookie = new object();
      _latestCookie = cookie;
      // These two values need to be created early (not on the lambda, which is on a different thread)
      _executor.Run(() => OnNextBackground(keptClient.Move(), cookie));
    }
  }

  private void OnNextBackground(KeptAlive<StatusOr<Client>> keptClient,
    object versionCookie) {
    using var cleanup1 = keptClient;
    var client = keptClient.Target;

    KeptAlive<StatusOr<TableHandle>> newState;
    try {
      var (cli, _) = client;
      var th = cli.Manager.FetchTable(_tableName);
      newState = KeepAlive.Register(StatusOr<TableHandle>.OfValue(th), client);
    } catch (Exception ex) {
      newState = KeepAlive.Register(StatusOr<TableHandle>.OfStatus(ex.Message));
    }
    using var cleanup2 = newState;

    lock (_sync) {
      if (!Object.ReferenceEquals(versionCookie, _latestCookie)) {
        return;
      }
      SetStateAndNotifyLocked(newState.Move());
    }
  }

  private static KeptAlive<StatusOr<TableHandle>> MakeState(string status) {
    var state = StatusOr<TableHandle>.OfStatus(status);
    return KeepAlive.Register(state);
  }

  private void SetStateAndNotifyLocked(KeptAlive<StatusOr<TableHandle>> newState) {
    Background666.InvokeDispose(_tableHandle);
    _tableHandle = newState;
    _observers.OnNext(newState.Target);
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
