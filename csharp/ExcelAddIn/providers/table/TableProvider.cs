using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/// <summary>
/// </summary>
internal class TableProvider :
  IValueObserverWithCancel<StatusOr<Client>>,
  IValueObserverWithCancel<StatusOr<DndClient>>,
  IValueObservable<StatusOr<TableHandle>> {
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
  private readonly StatusOrHolder<Client> _cachedClient = new(UnsetClientText);
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private readonly StatusOrHolder<TableHandle> _tableHandle = new(UnsetTableHandleText);

  public TableProvider(StateManager stateManager, EndpointId endpointId,
    PqName? pqName, string tableName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _tableHandle.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        if (_pqName != null) {
          var voc = ValueObserverWithCancelWrapper.Create<StatusOr<DndClient>>(
            this, _upstreamTokenSource.Token);
          _upstreamCallbacks = _stateManager.SubscribeToCorePlusClient(_endpointId, _pqName, voc);
        } else {
          var voc = ValueObserverWithCancelWrapper.Create<StatusOr<Client>>(
            this, _upstreamTokenSource.Token);
          _upstreamCallbacks = _stateManager.SubscribeToCoreClient(_endpointId, voc);
        }
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void RemoveObserver(IValueObserver<StatusOr<TableHandle>> observer) {
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

  public void OnNext(StatusOr<DndClient> client, CancellationToken token) {
    // Awkward work to turn StatusOr<DnClient> into StatusOr<Client>
    StatusOr<Client> newState;
    if (client.GetValueOrStatus(out var dndClient, out var status)) {
      newState = dndClient;
    } else {
      newState = status;
    }

    OnNext(newState, token);
  }

  public void OnNext(StatusOr<Client> client, CancellationToken token) {
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

      var progress = StatusOr<TableHandle>.OfTransient("Fetching Table");
      _tableHandle.ReplaceAndNotify(progress, _observers);

      // RefCounted item gets acquired on this thread.
      var shareDisposer = Repository.Share(cliRef);
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        // RefCounted item gets released on this thread.
        using var cleanup = shareDisposer;
        OnNextBackground(cliRef, backgroundToken);
      });
    }
  }

  private void OnNextBackground(Client client, CancellationToken token) {
    IDisposable? shareDisposer = null;
    StatusOr<TableHandle> result;
    try {
      var th = client.Manager.FetchTable(_tableName);
      // Keep a dependency on client
      shareDisposer = Repository.Register(th, client);
      result = th;
    } catch (Exception ex) {
      result = ex.Message;
    }
    using var cleanup = shareDisposer;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _tableHandle.ReplaceAndNotify(result, _observers);
    }
  }
}

public class Repository {
  public static IDisposable Register(IDisposable o, params IDisposable[] dependencies) {

  }

  public static IDisposable Share(IDisposable o) {

  }
}