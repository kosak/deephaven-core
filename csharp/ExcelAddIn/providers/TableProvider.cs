using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
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
  IValueObserver<StatusOr<RefCounted<Client>>>,
  IValueObserver<StatusOr<RefCounted<DndClient>>>,
  // IValueObservable<StatusOr<RefCounted<TableHandle>>>,
  // IDisposable,
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly object _sync = new();
  private readonly FreshnessSource _freshness;
  private readonly Latch _isSubscribed = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _tableHandle = UnsetTableHandleText;

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PqName? pqName, string tableName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
    _freshness = new(_sync);
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      SorUtil.AddObserverAndNotify(_observers, observer, _tableHandle, out _);
      if (_isSubscribed.TrySet()) {
        // Subscribe to parents at the time of the first subscription.
        _upstreamDisposer = _pqName != null
          ? _stateManager.SubscribeToCorePlusClient(_endpointId, _pqName, this)
          : _stateManager.SubscribeToCoreClient(_endpointId, this);
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
      Utility.ClearAndDispose(ref _upstreamDisposer);
      SorUtil.Replace(ref _tableHandle, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<RefCounted<Client>> client) {
    OnNextHelper(client);
  }

  public void OnNext(StatusOr<RefCounted<DndClient>> client) {
    OnNextHelper(client);
  }

  private void OnNextHelper<T>(StatusOr<RefCounted<T>> client) where T : Client {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      // Suppress responses from stale background workers.
      _freshness.Refresh();
      if (!client.GetValueOrStatus(out var cliRef, out var status)) {
        SorUtil.ReplaceAndNotify(ref _tableHandle, status, _observers);
        return;
      }

      SorUtil.ReplaceAndNotify(ref _tableHandle, "Fetching Table", _observers);
      var clientShare = cliRef.Share();
      Background.Run(() => {
        using var cleanup = clientShare;
        OnNextBackground(clientShare, _freshness.Current);
      });
    }
  }

  private void OnNextBackground<T>(RefCounted<T> client, FreshnessToken token)
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
      if (_isDisposed.Value || !token.IsCurrentUnsafe) {
        return;
      }
      SorUtil.ReplaceAndNotify(ref _tableHandle, newState, _observers);
    }
  }
}
