using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/// <summary>
/// This class has two functions, depending on whether the persistentQueryId is set.
/// If it is set, the class assumes that this is an Enterprise Core Plus table, and
/// follows that workflow.
/// 
/// Otherwise, if it is not set, the class assumes that this is a Community Core table,
/// and follows that workflow.
/// 
/// The job of this class is to subscribe to a PersistentQueryMapper with the key
/// (endpoint, pqName). Then, as that PQ mapper provider provides me with PqIds
/// with TableHandles (or status messages), process them.
/// If the message received was a status message, then forward it to my observers.
/// If it was a PqId, then [TODO]
/// 
/// If it was a TableHandle, then filter it by "condition" in the background, and provide
/// the resulting filtered TableHandle (or error) to my observers.
/// </summary>
internal class TableProvider :
  IValueObserverWithCancel<StatusOr<RefCounted<Client>>>,
  IValueObserverWithCancel<StatusOr<RefCounted<DndClient>>>,
  // IValueObservable<StatusOr<RefCounted<TableHandle>>>,
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _tableHandle = UnsetTableHandleText;

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PqName? pqName, string tableName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _tableHandle, out var isFirst);

      if (isFirst) {
        if (_pqName != null) {
          var voc = ValueObserverWithCancelWrapper.Create<StatusOr<RefCounted<DndClient>>>(
            this, _upstreamTokenSource.Token);
          _upstreamDisposer = _stateManager.SubscribeToCorePlusClient(_endpointId, _pqName, voc);
        } else {
          var voc = ValueObserverWithCancelWrapper.Create<StatusOr<RefCounted<Client>>>(
            this, _upstreamTokenSource.Token);
          _upstreamDisposer = _stateManager.SubscribeToCoreClient(_endpointId, voc);
        }
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _tableHandle, UnsetTableHandleText);
    }
  }

  public void OnNext(StatusOr<RefCounted<Client>> client, CancellationToken token) {
    OnNextHelper(client, token);
  }

  public void OnNext(StatusOr<RefCounted<DndClient>> client, CancellationToken token) {
    OnNextHelper(client, token);
  }

  private void OnNextHelper<T>(StatusOr<RefCounted<T>> client, CancellationToken token)
    where T : Client {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      // Suppress responses from stale background workers.
      if (!client.GetValueOrStatus(out var cliRef, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _tableHandle, status, _observers);
        return;
      }

      var progress = StatusOr<RefCounted<TableHandle>>.OfProgress("Fetching Table");
      StatusOrUtil.ReplaceAndNotify(ref _tableHandle, progress, _observers);
      // RefCounted item gets acquired on this thread.
      var clientShare = cliRef.Share();
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        // RefCounted item gets released on this thread.
        using var cleanup = clientShare;
        OnNextBackground(clientShare, backgroundToken);
      });
    }
  }

  private void OnNextBackground<T>(RefCounted<T> client, CancellationToken token)
    where T : Client {
    RefCounted<TableHandle>? newRef = null;
    StatusOr<RefCounted<TableHandle>> newState;
    try {
      var th = client.Value.Manager.FetchTable(_tableName);
      // Keep a dependency on client
      newRef = RefCounted.Acquire(th, client);
      newState = newRef;
    } catch (Exception ex) {
      newState = ex.Message;
    }
    using var cleanup = newRef;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _tableHandle, newState, _observers);
    }
  }
}
