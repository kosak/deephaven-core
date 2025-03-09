using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.Operations;

internal class SubscribeOperation : IExcelObservable,
  IValueObserver<StatusOr<RefCounted<TableHandle>>>,
  IObserver<TickingUpdate> {
  private const string UnsetTableData = "[No data]";
  private const string UnsetTickingSubscription = "[no ticking subscription]";
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private readonly FreshnessTokenSource _freshness;
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private IDisposable? _upstreamDisposer = null;
  private StatusOr<RefCounted<IDisposable>> _tickingSubscription = UnsetTickingSubscription;
  private StatusOr<object?[,]> _rendered = UnsetTableData;

  public SubscribeOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
    _freshness = new(_sync);
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    lock (_sync) {
      var wrappedObserver = new ExcelObserverWrapper(observer);
      _observers.AddAndNotify(wrappedObserver, _rendered, out var isFirst);

      if (isFirst) {
        if (_tableQuad.EndpointId != null) {
          _stateManager.EnsureConfig(_tableQuad.EndpointId);
        }
        _upstreamDisposer = _stateManager.SubscribeToTable(_tableQuad, this);
      }

      return ActionAsDisposable.Create(() => RemoveObserver(wrappedObserver));
    }
  }

  private void RemoveObserver(ExcelObserverWrapper observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _tickingSubscription, UnsetTickingSubscription);
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> tableHandle) {
    lock (_sync) {
      var token = _freshness.Refresh();
      StatusOrUtil.Replace(ref _tickingSubscription, UnsetTickingSubscription);

      if (!tableHandle.GetValueOrStatus(out var th, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _rendered, status, _observers);
        return;
      }

      var message = $"Subscribing to \"{_tableQuad.TableName}\"";
      StatusOrUtil.ReplaceAndNotify(ref _rendered, message, _observers);

      var thShare = th.Share();
      Background.Run(() => {
        using var cleanup = thShare;
        SubscribeInBackground(thShare, token);
      });
    }
  }

  private void SubscribeInBackground(RefCounted<TableHandle> tableHandle,
    FreshnessToken token) {
    RefCounted<IDisposable>? subRef = null;
    StatusOr<RefCounted<IDisposable>> result;
    try {
      var fobs = new ObserverFreshnessFilter<TickingUpdate>(this, token);
      var disposer = tableHandle.Value.Subscribe(fobs);
      // Hang on to the disposer, with a dependency on the tableHandle
      subRef = RefCounted.Acquire(disposer, tableHandle);
      result = subRef;
    } catch (Exception ex) {
      result = ex.Message;
    }

    using var cleanup = subRef;

    lock (_sync) {
      if (token.IsCurrent) {
        return;
      }
      StatusOrUtil.Replace(ref _tickingSubscription, result);
    }
  }

  public void OnNext(TickingUpdate update) {
    lock (_sync) {
      StatusOr<object?[,]> results;
      try {
        // When we fix the subscription API we will do this on a separate thread
        results = Renderer.Render(update.Current, _wantHeaders);
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
