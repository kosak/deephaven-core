using Deephaven.Dh_NetClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Refcounting;
using Deephaven.ExcelAddIn.Util;
using Utility = Deephaven.ExcelAddIn.Util.Utility;

namespace Deephaven.ExcelAddIn.Providers;

internal class SubscribeOperation :
  IValueObserverWithCancel<StatusOr<TableHandle>>,
  IObserverWithCancel<TickingUpdate>,
  IValueObservable<StatusOr<object?[,]>> {
  private const string UnsetTableHandle = "No TableHandle";
  private const string UnsetTableData = "No data";
  private const string UnsetTickingSubscription = "No ticking subscription";
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private IObservableCallbacks? _upstreamCallbacks = null;
  private readonly StatusOrHolder<TableHandle> _cachedTableHandle = new(UnsetTableHandle);
  private readonly StatusOrHolder<IDisposable> _tickingSubscription = new(UnsetTickingSubscription);
  private readonly StatusOrHolder<object?[,]> _rendered = new(UnsetTableData);

  public SubscribeOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<object?[,]>> observer) {
    lock (_sync) {
      _rendered.AddObserverAndNotify(_observers, observer, out var isFirst);

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
      _cachedTableHandle.Replace(UnsetTableHandle);
      _tickingSubscription.Replace(UnsetTickingSubscription);
    }
  }

  public void OnNext(StatusOr<TableHandle> tableHandle,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      _cachedTableHandle.Replace(tableHandle);
      OnNextHelper();
    }
  }

  private void OnNextHelper() {
    lock (_sync) {
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      _tickingSubscription.Replace(UnsetTickingSubscription);

      if (!_cachedTableHandle.GetValueOrStatus(out var th, out var status)) {
        _rendered.ReplaceAndNotify(status, _observers);
        return;
      }

      var progress = StatusOr<object?[,]>.OfTransient($"Subscribing to \"{_tableQuad.TableName}\"");
      _rendered.ReplaceAndNotify(progress, _observers);

      // RefCounted item gets acquired on this thread.
      var sharedDisposer = Repository.Share(th);
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        // RefCounted item gets released on this thread.
        using var cleanup = sharedDisposer;
        SubscribeInBackground(th, backgroundToken);
      });
    }
  }

  private void SubscribeInBackground(TableHandle tableHandle, CancellationToken token) {
    IDisposable? sharedDisposer = null;
    StatusOr<IDisposable> result;

    IObserver<TickingUpdate> tuo;
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      tuo = ObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
    }

    try {
      var subDisposer = tableHandle.Subscribe(tuo);
      // Hang on to the disposer, with a dependency on the tableHandle
      sharedDisposer = Repository.Register(subDisposer, tableHandle);
      result = StatusOr<IDisposable>.OfValue(subDisposer);
    } catch (Exception ex) {
      result = ex.Message;
    }

    using var cleanup = sharedDisposer;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _tickingSubscription.Replace(result);
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
        _rendered.ReplaceAndNotify(results, _observers);
      } catch (Exception e) {
        results = e.Message;
      }
      _rendered.ReplaceAndNotify(results, _observers);
    }
  }

  public void OnError(Exception ex, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _rendered.ReplaceAndNotify(ex.Message, _observers);
    }
  }

  public void OnCompleted(CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _rendered.ReplaceAndNotify("Subscription closed", _observers);
    }
  }
}
