using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/// <summary>
/// </summary>
internal class TableProvider :
  IValueObserverWithCancel<StatusOr<RefCounted<Client>>>,
  IValueObserverWithCancel<StatusOr<RefCounted<DndClient>>>,
  IValueObservable<StatusOr<RefCounted<TableHandle>>> {
  private const string UnsetTableHandleText = "No TableHandle";
  private const string UnsetClientText = "No Client";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IObservableCallbacks? _upstreamCallbacks = null;
  private readonly StatusOrHolder<RefCounted<Client>> _cachedClient = new(UnsetClientText);
  private readonly ObserverContainer<StatusOr<RefCounted<TableHandle>>> _observers = new();
  private readonly StatusOrHolder<RefCounted<TableHandle>> _tableHandle = new(UnsetTableHandleText);

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PqName? pqName, string tableName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      _tableHandle.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        if (_pqName != null) {
          var voc = ValueObserverWithCancelWrapper.Create<StatusOr<RefCounted<DndClient>>>(
            this, _upstreamTokenSource.Token);
          _upstreamCallbacks = _stateManager.SubscribeToCorePlusClient(_endpointId, _pqName, voc);
        } else {
          var voc = ValueObserverWithCancelWrapper.Create<StatusOr<RefCounted<Client>>>(
            this, _upstreamTokenSource.Token);
          _upstreamCallbacks = _stateManager.SubscribeToCoreClient(_endpointId, voc);
        }
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    lock (_sync) {
      if (!_cachedClient.GetValueOrStatus(out _, out _)) {
        // Parent is in error state, so propagate retry to parent.
        _upstreamCallbacks?.Retry();
      } else {
        // Parent is in healthy state, so retry here.
        OnNextHelper();
      }
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamCallbacks);
      _tableHandle.Replace(UnsetTableHandleText);
      _cachedClient.Replace(UnsetClientText);
    }
  }

  public void OnNext(StatusOr<RefCounted<DndClient>> client, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Awkward work to turn RefCounted<DnClient> into RefCounted<Client>
      RefCounted<Client>? newRef = null;
      StatusOr<RefCounted<Client>> newState;
      if (client.GetValueOrStatus(out var dndClient, out var status)) {
        newRef = RefCounted<Client>.CastAndShare(dndClient);
        newState = newRef;
      } else {
        newState = status;
      }

      using var cleanup = newRef;

      _cachedClient.Replace(newState);
      OnNextHelper();
    }
  }

  public void OnNext(StatusOr<RefCounted<Client>> client, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      _cachedClient.Replace(client);

      OnNextHelper();
    }
  }

  private void OnNextHelper() {
    lock (_sync) {
      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      // Suppress responses from stale background workers.
      if (!_cachedClient.GetValueOrStatus(out var cliRef, out var status)) {
        _tableHandle.ReplaceAndNotify(status, _observers);
        return;
      }

      var progress = StatusOr<RefCounted<TableHandle>>.OfTransient("Fetching Table");
      _tableHandle.ReplaceAndNotify(progress, _observers);

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

  private void OnNextBackground(Client client, CancellationToken token) {
    IDisposable? disposer = null;
    StatusOr<TableHandle> newState;
    try {
      var th = client.Manager.FetchTable(_tableName);
      // Keep a dependency on client
      disposer = Repository.Register(th, client);
      newState = th;
    } catch (Exception ex) {
      newState = ex.Message;
    }
    using var cleanup = disposer;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _tableHandle.ReplaceAndNotify(newState, _observers);
    }
  }
}

public class Repository {
  public static IDisposable Register(object o, params object[] dependencies) {

  }
}