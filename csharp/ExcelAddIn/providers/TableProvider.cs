using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableProvider :
  IObserver<RefCounted<StatusOr<Client>>>,
  // IObservable<StatusOr<TableHandle>>, // redundant, part of ITableProvider
  ITableProvider {
  private const string UnsetTableHandleText = "[No Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private Action? _onDispose;
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private RefCounted<StatusOr<TableHandle>> _tableHandle =
    RefCounted.Acquire(StatusOr<TableHandle>.OfStatus(UnsetTableHandleText));

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, Action onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _onDispose = onDispose;
  }

  public void Start() {
    _upstreamDisposer = _persistentQueryId != null
      ? _stateManager.SubscribeToPersistentQuery(_endpointId, _persistentQueryId, this)
      : _stateManager.SubscribeToCoreClient(_endpointId, this);
  }

  public IDisposable Subscribe(IObserver<RefCounted<StatusOr<TableHandle>>> observer) {
    // Locked because I want these to happen together.
    lock (_syncRoot) {
      _observers.Add(observer, out _);
      _observers.ZamboniOneNext(observer, _filteredTableHandle);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<RefCounted<StatusOr<TableHandle>>> observer) {
    _observers.Remove(observer, out var isLast);
    if (!isLast) {
      return;
    }

    IDisposable disp1;
    IDisposable disp2;
    lock (_syncRoot) {
      disp1 = Utility.Release(ref _upstreamDisposer);
      disp2 = Utility.Release(ref _onDispose);
    }

    Utility.DisposeInBackground(disp1);
    Utility.DisposeInBackground(disp2);
    DisposeTableHandleState("Disposing TableHandle");
  }

  private void DisposeTableHandleState(string statusMessage) {
    var state = RefCounted.Acquire(StatusOr<TableHandle>.OfStatus(statusMessage));
    lock (_syncRoot) {
      Utility.Swap(ref _tableHandle, ref state);
    }
    _observers.Send(_tableHandle);
    Utility.DisposeInBackground(state);
  }

  public void OnNext(RefCounted<StatusOr<Client>> client) {
    DisposeTableHandleState($"Fetching \"{_tableName}\"");
    var versionCookie = new object();
    lock (_syncRoot) {
      _latestCookie.SetValue(versionCookie);
    }
    Utility.RunInBackground(() => OnNextBackground(versionCookie, client));
  }

  private void OnNextBackground(object versionCookie,
    RefCounted<StatusOr<Client>> client) {
    // If the new state is just a status message, make that our state
    // and transmit to our observers
    StatusOr<TableHandle> result;
    if (!client.Value.GetValueOrStatus(out var cli, out var status)) {
      result = StatusOr<TableHandle>.OfStatus(status);
    } else {
      try {
        var th = cli.Manager.FetchTable(_tableName);
        result = StatusOr<TableHandle>.OfValue(th);
      } catch (Exception ex) {
        result = StatusOr<TableHandle>.OfStatus(ex.Message);
      }
    }

    // The derived table handle has a sharing dependency on the Client
    var state = RefCounted.Acquire(result, client.Share());

    lock (_syncRoot) {
      if (object.ReferenceEquals(versionCookie, _latestCookie)) {
        Utility.Swap(ref _tableHandle, ref state);
        _observers.Enqueue(_tableHandle.Share());
      }
    }

    state.Dispose();
    client.Dispose();
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
