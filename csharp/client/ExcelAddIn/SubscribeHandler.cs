using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class SusbcribeHandler : ISuperNubbin, ITickingCallback {
  private readonly Lender<ClientOrStatus> _clientLender;
  private readonly string _tableName;
  private readonly TableFilter _filter;

  public SusbcribeHandler(Lender<ClientOrStatus> clientLender, string tableName, TableFilter filter) {
    _clientLender = clientLender;
    _tableName = tableName;
    _filter = filter;
  }

  public void Refresh(IStatusObserver statusObserver) {
    // called when I get a new client. I should unsub the old one then subscribe here, both in a separate task.
    throw new NotImplementedException();
  }

  public void OnNewObserver(IExcelObserver observer, bool isFirstObserver, IStatusObserver statusObserver) {
    if (!isFirstObserver) {
      return;
    }

    Task.Run(() => PerformSubscription(statusObserver));
  }

  public void OnLastObserverRemoved() {
    // I could unsub here
    throw new NotImplementedException();
  }

  private void PerformSubscription(IStatusObserver statusObserver) {
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
      var subHandle = th.Subscribe(this);
      lock (_sync) {
        _subscriptionHandle = subHandle;
      }
    } catch (Exception ex) {
      statusObserver.OnError(ex);
    }
  }
}
