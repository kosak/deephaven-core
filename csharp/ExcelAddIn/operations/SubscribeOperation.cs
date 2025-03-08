using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.Operations;

internal class SubscribeOperation : IExcelObservable,
  IValueObserver<StatusOr<RefCounted<TableHandle>>> {
  private const string UnsetTableData = "[No data]";
  private readonly TableQuad _tableQuad;
  private readonly bool _wantHeaders;
  private readonly StateManager _stateManager;
  private readonly object _sync = new();
  private readonly ObserverContainer<StatusOr<object?[,]>> _observers = new();
  private IDisposable? _tableDisposer = null;
  private IDisposable? _currentSub666Disposer = null;
  private StatusOr<object?[,]> _rendered = UnsetTableData;


  public SubscribeOperation(TableQuad tableQuad, bool wantHeaders, StateManager stateManager) {
    _tableQuad = tableQuad;
    _wantHeaders = wantHeaders;
    _stateManager = stateManager;
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    lock (_sync) {
      var wrappedObserver = new ExcelObserverWrapper(observer);
      _observers.AddAndNotify(wrappedObserver, _rendered, out var isFirst);

      if (isFirst) {
        _tableDisposer = _stateManager.SubscribeToTable(_tableQuad, this);
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

      Utility.ClearAndDispose(ref _tableDisposer);
    }
  }

  public void OnNext(StatusOr<RefCounted<TableHandle>> tableHandle) {
    lock (_sync) {
      var token = _freshness.Refresh();
      Background.ClearAndDispose(ref _subRef);

      if (!tableHandle.GetValueOrStatus(out var th, out var status)) {
        SorUtil.ReplaceAndNotify(ref _rendered, status, _observers);
        return;
      }

      var message = $"Subscribing to \"{_tableQuad.TableName}\"";
      SorUtil.ReplaceAndNotify(ref _rendered, message, _observers);

      var thShare = th.Share();
      Background.Run(() => {
        using var cleanup = thShare;
        SubscribeInBackground(thShare, token);
      });
    }
  }

  private void SubscribeInBackground(RefCounted<TableHandle> tableHandle,
    FreshnessToken token) {

  }

  _currentTableHandle = tableHandle;
      _currentSubDisposer = _currentTableHandle.Subscribe(new MyTickingObserver(_observers, _wantHeaders));

      try {
        using var ct = tableHandle.ToClientTable();
        var result = Renderer.Render(ct, _wantHeaders);
        _observers.SendValue(result);
      } catch (Exception ex) {
        _observers.SendStatus(ex.Message);
      }
    }
  }

  private class MyTickingObserver : IObserver<TickingUpdate> {
    private readonly ObserverContainer<StatusOr<object?[,]>> _observers;
    private readonly bool _wantHeaders;

    public MyTickingObserver(ObserverContainer<StatusOr<object?[,]>> observers,
      bool wantHeaders) {
      _observers = observers;
      _wantHeaders = wantHeaders;
    }

    public void OnNext(TickingUpdate update) {
      try {
        var results = Renderer.Render(update.Current, _wantHeaders);
        _observers.SendValue(results);
      } catch (Exception e) {
        _observers.SendStatus(e.Message);
      }
    }

    public void OnError(Exception ex) {
      _observers.SendStatus(ex.Message);
    }

    public void OnCompleted() {
      // Even though my subscription has ended, my observers may get more data
      // from some future subscription. So at this point they only get a message.
      _observers.SendStatus("Subscription closed");
    }
  }
}
