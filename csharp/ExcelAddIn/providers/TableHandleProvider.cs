using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deephaven.ExcelAddIn.ExcelDna;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Deephaven.ExcelAddIn.Providers;

internal class TableHandleProvider(WorkerThread workerThread, TableTriple descriptor, string filter) :
      IObserver<StatusOr<SessionBase>>, IObserver<StatusOr<Client>>, IObservable<StatusOr<TableHandle>>, IDisposable {
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private IDisposable? _pqDisposable = null;
  private StatusOr<TableHandle> _tableHandle = StatusOr<TableHandle>.OfStatus("[no TableHandle]");

  public void OnNext(StatusOr<SessionBase> session) {
    if (workerThread.InvokeIfRequired(() => OnNext(session))) {
      return;
    }

    try {
      DisposePqAndThState();

      if (!session.GetValueOrStatus(out var sb, out var status)) {
        _observers.SendStatus(status);
        return;
      }

      // Visit needs a return type and value, so we return (object)null
      _ = sb.Visit(coreSession => {
        this.SendValue(coreSession.Client);
        return Unit.Instance;
      }, corePlusSession => {
        var pqid = descriptor.PersistentQueryId;
        _observers.SendStatus($"Subscribing to PQ \"{pqid}\"");
        _pqDisposable = corePlusSession.SubscribeToPq(pqid, this);
        return Unit.Instance;
      });
    } catch (Exception ex) {
      _observers.SendStatus(ex.Message);
    }
  }

  public void OnNext(StatusOr<Client> client) {
    if (workerThread.InvokeIfRequired(() => OnNext(client))) {
      return;
    }

    try {
      DisposePqAndThState();

      if (!client.GetValueOrStatus(out var cli, out var status)) {
        _observers.SetAndSendStatus(ref _tableHandle, status);
        return;
      }

      _observers.SetAndSendStatus(ref _tableHandle, $"Fetching \"{descriptor.TableName}\"");

      var th = cli.Manager.FetchTable(descriptor.TableName);
      if (filter != "") {
        var temp = th;
        th = temp.Where(filter);
        temp.Dispose();
      }

      _observers.SetAndSendValue(ref _tableHandle, th);
    } catch (Exception ex) {
      _observers.SetAndSendStatus(ref _tableHandle, ex.Message);
    }
  }

  private void DisposePqAndThState() {
    _ = _tableHandle.GetValueOrStatus(out var oldTh, out var _);
    var oldPq = Utility.Exchange(ref _pqDisposable, null);

    if (oldTh != null) {
      _observers.SetAndSendStatus(ref _tableHandle, "Disposing TableHandle");
      oldTh.Dispose();
    }

    if (oldPq != null) {
      _observers.SetAndSendStatus(ref _tableHandle, "Disposing PQ");
      oldPq.Dispose();
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
