using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class SubscribeHandler : ISuperNubbin {
  private readonly Lender<ClientOrStatus> _clientLender;
  private readonly string _tableName;
  private readonly TableFilter _filter;
  private readonly object _sync = new();
  private TableHandle? _currentTableHandle;
  private SubscriptionHandle? _currentSubHandle;

  public SubscribeHandler(Lender<ClientOrStatus> clientLender, string tableName, TableFilter filter) {
    _clientLender = clientLender;
    _tableName = tableName;
    _filter = filter;
  }

  public void Refresh(IStatusObserver statusObserver) {
    Task.Run(() => PerformSubscribe(statusObserver));
  }

  public void OnNewObserver(IExcelObserver observer, bool isFirstObserver, IStatusObserver statusObserver) {
    if (isFirstObserver) {
      Task.Run(() => PerformSubscribe(statusObserver));
    }
  }

  public void OnLastObserverRemoved() {
    var (oldTh, oldHandle) = Swappytown(null, null);
    Task.Run(() => PerformUnsubscribe(oldTh, oldHandle));
  }

  private void PerformUnsubscribe(TableHandle? th, SubscriptionHandle? subHandle) {
    try {
      if (th == null) {
        return;
      }
      th.Unsubscribe(subHandle!);
    } catch(Exception ex) {
      Debug.WriteLine(ex);
    }
  }

  private void PerformSubscribe(IStatusObserver statusObserver) {
    var (oldTh, oldHandle) = Swappytown(null, null);
    PerformUnsubscribe(oldTh, oldHandle);
    try {
      using var borrowed = _clientLender.Borrow();
      var cos = borrowed.Value;
      if (cos.Status != null) {
        statusObserver.OnStatus(cos.Status);
        return;
      }

      if (cos.Client == null) {
        return;
      }

      var th = cos.Client.Manager.FetchTable(_tableName);
      var subHandle = th.Subscribe(new ZamboniSuperfreak(statusObserver));
      (oldTh, oldHandle) = Swappytown(th, subHandle);
      // I don't know why these values would ever be non-null, but unsubscribe if they are
      PerformUnsubscribe(oldTh, oldHandle);
    } catch (Exception ex) {
      statusObserver.OnError(ex);
    }
  }

  private Tuple<TableHandle?, SubscriptionHandle?> Swappytown(TableHandle? th, SubscriptionHandle? subHandle) {
    lock (_sync) {
      var result = Tuple.Create(_currentTableHandle, _currentSubHandle);
      _currentTableHandle = th;
      _currentSubHandle = subHandle;
      return result;
    }
  }
}

class ZamboniSuperfreak : ITickingCallback {
  private readonly IStatusObserver _statusObserver;

  public ZamboniSuperfreak(IStatusObserver statusObserver) => _statusObserver = statusObserver;

  public void OnTick(TickingUpdate update) {
    try {
      var results = Renderer.Render(update.Current);
      _statusObserver.OnNext(results);
    } catch (Exception ex) {
      _statusObserver.OnError(ex);
    }
  }

  public void OnFailure(string errorText) {
    throw new NotImplementedException();
  }
}
