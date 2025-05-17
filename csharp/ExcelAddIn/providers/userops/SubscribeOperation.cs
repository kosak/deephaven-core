using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Operations;

internal class SubscribeOperation : 
  IValueObserverWithCancel<StatusOr<RefCounted<TableHandle>>>,
  IValueObservable<StatusOr<object?[,]>> {
  private const string UnsetTableData = "[No data]";
  private const string UnsetTickingSubscription = "[no ticking subscription]";
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private IDisposable? _upstreamDisposer = null;
  private StatusOr<RefCounted<IDisposable>> _tickingSubscription = UnsetTickingSubscription;
  private StatusOr<object?[,]> _rendered = UnsetTableData;

  public SubscribeOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<Object?[,]>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _rendered, out var isFirst);

      if (isFirst) {
        if (_tableQuad.EndpointId != null) {
          _stateManager.EnsureConfig(_tableQuad.EndpointId);
        }
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamDisposer = _stateManager.SubscribeToTable(_tableQuad, voc);
      }

      return ActionAsDisposable.Create(() => RemoveObserver(observer));
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<Object?[,]>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _tickingSubscription, UnsetTickingSubscription);
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> tableHandle,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      StatusOrUtil.Replace(ref _tickingSubscription, UnsetTickingSubscription);

      if (!tableHandle.GetValueOrStatus(out var th, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _rendered, status, _observers);
        return;
      }

      var message = $"Subscribing to \"{_tableQuad.TableName}\"";
      StatusOrUtil.ReplaceAndNotify(ref _rendered, message, _observers);

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

    TickingUpdateObserver tuo;
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      tuo = new TickingUpdateObserver(this, _upstreamTokenSource.Token);
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

  private class TickingUpdateObserver : IObserver<TickingUpdate> {
    private readonly SubscribeOperation _owner;
    private readonly CancellationToken _token;

    public TickingUpdateObserver(SubscribeOperation owner, CancellationToken token) {
      _owner = owner;
      _token = token;
    }

    public void OnNext(TickingUpdate update) {
      lock (_owner._sync) {
        StatusOr<object?[,]> results;
        try {
          // When we fix the subscription API we will do this on a separate thread
          results = Renderer.Render(update.Current, _owner._wantHeaders);
          StatusOrUtil.ReplaceAndNotify(ref _rendered, results, _observers);
        } catch (Exception e) {
          results = e.Message;
        }
        StatusOrUtil.ReplaceAndNotify(ref _rendered, results, _observers);
      }
    }

    public void OnError(Exception ex) {
      lock (_sync) {
        StatusOrUtil.ReplaceAndNotify(ref _rendered, ex.Message, _observers);
      }
    }

    public void OnCompleted() {
      lock (_sync) {
        StatusOrUtil.ReplaceAndNotify(ref _rendered, "Subscription closed", _observers);
      }
    }
  }
}
