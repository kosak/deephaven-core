using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableProvider :
  IObserver<StatusOrCounted<Client>>,
  // IObservable<StatusOrCounted<TableHandle>>, // redundant, part of ITableProvider
  ITableProvider {
  private const string UnsetTableHandleText = "[No Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private Action? _onDispose;
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOrCounted<TableHandle> _tableHandle =
    StatusOrCounted<TableHandle>.OfStatus(UnsetTableHandleText);

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

  public IDisposable Subscribe(IObserver<StatusOrCounted<TableHandle>> observer) {
    // Locked because I want these to happen together.
    lock (_syncRoot) {
      _observers.Add(observer, out _);
      _observers.ZamboniOneNext(observer, _filteredTableHandle.Share());
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOrCounted<TableHandle>> observer) {
    _observers.Remove(observer, out var isLast);
    if (!isLast) {
      return;
    }

    lock (_syncRoot) {
      ZZTop.ClearAndDisposeInBackground(ref _upstreamDisposer);
      ZZTop.ClearAndDisposeInBackground(ref _onDispose);
    }
    DisposeTableHandleState("Disposing TableHandle");
  }

  private void DisposeTableHandleState(string statusMessage) {
    lock (_syncRoot) {
      StatusOrCounted.ResetWithStatus(ref _tableHandle, statusMessage);
      _observers.Send(_tableHandle.Share());
    }
  }

  public void OnNext(StatusOrCounted<TableHandle> client) {
    using var cleanup = client;
    if (!client.GetValueOrStatus(out _, out var status)) {
      DisposeTableHandleState(status);
      return;
    }

    DisposeTableHandleState($"Fetching \"{_tableName}\"");
    var versionCookie = _versionParty.Mark();
    Utility.RunInBackground(() => OnNextBackground(versionCookie, client.Share()));
  }

  private void OnNextBackground(object versionCookie,
    StatusOrCounted<Client> client) {
    using var cleanup1 = client;

    StatusOrCounted<TableHandle> result;
    try {
      var th = client.Value.Manager.FetchTable(_tableName);
      // This keeps the dependencies (client) alive as well.
      result = StatusOrCounted<TableHandle>.OfValue(th, client.Share());
    } catch (Exception ex) {
      result = StatusOrCounted<TableHandle>.OfStatus(ex.Message);
    }
    using var cleanup2 = result;

    versionCookie.Finish(() => {
      StatusOrCounted.Replace(ref _tableHandle, result.Share());
      _observers.Enqueue(result.Share());
    });
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
