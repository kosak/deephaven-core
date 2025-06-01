using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using System.Reflection.Metadata.Ecma335;

namespace Deephaven.ExcelAddIn.Providers;

internal class SubscribeOperation :
  IValueObserverWithCancel<StatusOr<RefCounted<TableHandle>>>,
  IObserverWithCancel<TickingUpdate>,
  IValueObservable<StatusOr<object?[,]>> {
  private const string UnsetTableHandle = "[No TableHandle]";
  private const string UnsetTableData = "[No data]";
  private const string UnsetTickingSubscription = "[no ticking subscription]";
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private IObservableCallbacks? _upstreamCallbacks = null;
  private StatusOr<RefCounted<TableHandle>> _cachedTableHandle = UnsetTableHandle;
  private StatusOr<RefCounted<IDisposable>> _tickingSubscription = UnsetTickingSubscription;
  private StatusOr<object?[,]> _rendered = UnsetTableData;

  public SubscribeOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<object?[,]>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _rendered, out var isFirst);

      if (isFirst) {
        if (_tableQuad.EndpointId != null) {
          _stateManager.EnsureConfig(_tableQuad.EndpointId);
        }
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamCallbacks = _stateManager.SubscribeToTable(_tableQuad, voc);
      }

      return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
    }
  }

  private void Retry() {
    lock (_sync) {
      // Ask the parent to Retry, regardless of whether our cached TableHandle is
      // healthy or in an error state.
      _upstreamCallbacks?.Retry();
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<object?[,]>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamCallbacks);
      StatusOrUtil.Replace(ref _cachedTableHandle, UnsetTableHandle);
      StatusOrUtil.Replace(ref _tickingSubscription, UnsetTickingSubscription);
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> tableHandle,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      StatusOrUtil.Replace(ref _cachedTableHandle, tableHandle);
      OnNextHelper();
    }
  }

  private void OnNextHelper() {
    lock (_sync) {
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      StatusOrUtil.Replace(ref _tickingSubscription, UnsetTickingSubscription);

      if (!_cachedTableHandle.GetValueOrStatus(out var th, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _rendered, status, _observers);
        return;
      }

      var progress = StatusOr<object?[,]>.OfTransient($"Subscribing to \"{_tableQuad.TableName}\"");
      StatusOrUtil.ReplaceAndNotify(ref _rendered, progress, _observers);

      var thShare = th.Share();
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        using var cleanup = thShare;
        SubscribeInBackground(thShare, backgroundToken);
      });
    }
  }

  private void SubscribeInBackground(RefCounted<TableHandle> tableHandle,
    CancellationToken token) {
    RefCounted<IDisposable>? subRef = null;
    StatusOr<RefCounted<IDisposable>> result;

    IObserver<TickingUpdate> tuo;
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      tuo = ObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
    }

    try {
      var disposer = tableHandle.Value.Subscribe(tuo);
      // Hang on to the disposer, with a dependency on the tableHandle
      subRef = RefCounted.Acquire(disposer, tableHandle);
      result = subRef;
    } catch (Exception ex) {
      result = ex.Message;
    }

    using var cleanup = subRef;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.Replace(ref _tickingSubscription, result);
    }
  }

  public void OnNext(TickingUpdate update, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOr<object?[,]> results;
      try {
        // TODO(kosak): fix the subscription API to do an Immer-like sharing
        // data structure, then render this on a separate thread rather than blocking
        // the callback thread while you render.
        results = Renderer.Render(update.Current, _wantHeaders);
        StatusOrUtil.ReplaceAndNotify(ref _rendered, results, _observers);
      } catch (Exception e) {
        results = e.Message;
      }
      StatusOrUtil.ReplaceAndNotify(ref _rendered, results, _observers);
    }
  }

  public void OnError(Exception ex, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _rendered, ex.Message, _observers);
    }
  }

  public void OnCompleted(CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _rendered, "Subscription closed", _observers);
    }
  }
}
