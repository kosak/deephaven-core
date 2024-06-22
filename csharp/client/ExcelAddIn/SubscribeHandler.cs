using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class SusbcribeHandler : ISuperNubbin {
  private readonly Lender<ClientOrStatus> _clientLender;
  private readonly string _tableName;
  private readonly TableFilter _filter;

  public SusbcribeHandler(Lender<ClientOrStatus> clientLender, string tableName, TableFilter filter) {
    _clientLender = clientLender;
    _tableName = tableName;
    _filter = filter;
  }

  public void Refresh(IStatusObserver statusObserver) {
    Task.Run(() => PerformSubscriptionTasks(true, statusObserver));
  }

  public void OnNewObserver(IExcelObserver observer, bool isFirstObserver, IStatusObserver statusObserver) {
    if (isFirstObserver) {
      Task.Run(() => PerformSubscriptionTasks(true, statusObserver));
    }
  }

  public void OnLastObserverRemoved() {
    Task.Run(() => PerformSubscriptionTasks(false, BUT_THERE_IS_NO_OBSERVER));
  }

  private void PerformSubscriptionTasks(bool wantSubscribe, IStatusObserver statusObserver) {
    try {
      // First do the unsubscribe step
      lock (_sync) {
        oldTable = _subscriptionTable;
        oldHandle = _subscriptionHandle;
        _subscriptionTable = null;
        _subscriptionHandle = null;
      }

      if (oldTable != null) {
        oldTable.Unsubscribe(oldHandle);
        oldTable.Dispose();
      }

      if (!wantSubscribe) {
        return;
      }

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
      lock (_sync) {
        _subscriptionTable = th;
        _subscriptionHandle = subHandle;
      }
    } catch (Exception ex) {
      statusObserver.OnError(ex);
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
