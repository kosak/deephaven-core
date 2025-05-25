using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Auth;

namespace Deephaven.ExcelAddIn.Providers;

/// <summary>
/// </summary>
internal class TableProvider :
  IValueObserverWithCancel<StatusOr<RefCounted<Client>>>,
  IValueObserverWithCancel<StatusOr<RefCounted<DndClient>>>,
  IValueObserverWithCancel<RetryPlaceholder>,
  // IValueObservable<StatusOr<RefCounted<TableHandle>>>,
  ITableProviderBase {
  private const string UnsetTableHandleText = "[No Table]";
  private const string UnsetClientText = "[No Client]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IDisposable? _upstreamDisposer = null;
  private IDisposable? _retryDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _tableHandle = UnsetTableHandleText;
  private StatusOr<RefCounted<Client>> _cachedClient = UnsetClientText;

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
        var rtvoc = ValueObserverWithCancelWrapper.Create<RetryPlaceholder>(
          this, _upstreamTokenSource.Token);
        _retryDisposer = _stateManager.SubscribeToRetry(_endpointId, _pqName, _tableName, rtvoc);
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
      Utility.ClearAndDispose(ref _retryDisposer);
      StatusOrUtil.Replace(ref _tableHandle, UnsetTableHandleText);
      StatusOrUtil.Replace(ref _cachedClient, UnsetClientText);
    }
  }

  public void OnNext(StatusOr<RefCounted<DndClient>> client, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Awkward work to turn RefCounted<DnClient> into RefCounted<Client>
      StatusOr<RefCounted<Client>> temp;
      if (client.GetValueOrStatus(out var dndClient, out var status)) {
        temp = RefCounted<Client>.CastFrom(dndClient);
      } else {
        temp = status;
      }
      StatusOrUtil.Replace(ref _cachedClient, temp);
      OnNextHelper(temp, token);
    }
  }

  public void OnNext(RetryPlaceholder _, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      OnNextHelper(_cachedClient, token);
    }
  }

  public void OnNext(StatusOr<RefCounted<Client>> client, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Keep a cached copy of Client (needed for Retry)
      StatusOrUtil.Replace(ref _cachedClient, client);

      OnNextHelper(client, token);
    }
  }

  private void OnNextHelper(StatusOr<RefCounted<Client>> client, CancellationToken token) {
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

      var progress = StatusOr<RefCounted<TableHandle>>.OfTransient("Fetching Table");
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

  private void OnNextBackground(RefCounted<Client> client, CancellationToken token) {
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
