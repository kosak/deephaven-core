using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableProvider :
  IObserver<StatusOr<View<Client>>>,
  IObservable<StatusOr<View<TableHandle>>> {
  private const string UnsetTableHandleText = "[No Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly object _sync = new();
  private IDisposable? _onDispose;
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<View<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _tableHandle =
    StatusOr<RefCounted<TableHandle>>.OfStatus(UnsetTableHandleText);
  private object _latestCookie = new();

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, IDisposable? onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _onDispose = onDispose;

    // Do my subscriptions on a separate thread to avoid rentrancy on StateManager
    Background.Run(Start);
  }

  private void Start() {
    var temp = _persistentQueryId != null
      ? _stateManager.SubscribeToPersistentQuery(_endpointId, _persistentQueryId, this)
      : _stateManager.SubscribeToCoreClient(_endpointId, this);

    lock (_sync) {
      _upstreamDisposer = temp;
    }
  }

  public IDisposable Subscribe(IObserver<StatusOr<View<TableHandle>>> observer) {
    lock (_sync) {
      _observers.Add(observer, out _);
      _observers.OnNextOne(observer, _tableHandle.AsView(),
        _tableHandle.Share().AsDisposable());
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<View<TableHandle>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }
      Background.Dispose(Utility.Exchange(ref _upstreamDisposer, null));
      Background.Dispose(Utility.Exchange(ref _onDispose, null));
    }
    ResetTableHandleStateAndNotify("Disposing TableHandle");
  }

  private void ResetTableHandleStateAndNotify(string statusMessage) {
    lock (_sync) {
      StatusOrCounted.ReplaceWithStatus(ref _tableHandle, statusMessage);
      _observers.OnNext(_tableHandle.AsView(),
        _tableHandle.Share().AsDisposable());
    }
  }

  public void OnNext(StatusOr<View<Client>> client) {
    if (!client.GetValueOrStatus(out var cli, out var status)) {
      ResetTableHandleStateAndNotify(status);
      return;
    }

    ResetTableHandleStateAndNotify("Fetching Table");
    // Share here while still on this thread. (Sharing inside the lambda is too late).
    lock (_sync) {
      // Need these two values created in this thread (not in the body of the lambda).
      var cookie = new object();
      var sharedClient = cli.Share();
      _latestCookie = cookie;
      Background.Run(() => OnNextBackground(cookie, sharedClient));
    }
  }

  private void OnNextBackground(object versionCookie, RefCounted<Client> client) {
    using var cleanup1 = client;

    var newTable = StatusOrCounted.Empty<TableHandle>();
    try {
      var th = client.Value.Manager.FetchTable(_tableName);
      // This keeps the dependencies (namely, the client) alive as well.
      StatusOrCounted.ReplaceWithValue(ref newTable, th, client.Share());
    } catch (Exception ex) {
      StatusOrCounted.ReplaceWithStatus(ref newTable, ex.Message);
    }
    using var cleanup2 = newTable.AsDisposable();

    lock (_sync) {
      if (!Object.ReferenceEquals(versionCookie, _latestCookie)) {
        return;
      }
      StatusOrCounted.ReplaceWith(ref _tableHandle, newTable.AsView());
      _observers.OnNext(_tableHandle.AsView(), _tableHandle.Share().AsDisposable());
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
