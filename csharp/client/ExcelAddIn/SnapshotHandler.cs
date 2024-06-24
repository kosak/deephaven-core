using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

public sealed class SuperNubbin666 : IStatusObserver {
  private readonly IExcelObserver[] _observers;

  public SuperNubbin666(IExcelObserver[] observers) => _observers = observers;

  public void OnNext(object?[,] result) {
    foreach (var observer in _observers) {
      observer.OnNext(result);
    }
  }
}

public sealed class Unit {
}



internal class SnapshotHandler : ISuperNubbin {
  private readonly Lender<ClientOrStatus> _clientLender;
  private readonly string _tableName;
  private readonly TableFilter _filter;

  public SnapshotHandler(Lender<ClientOrStatus> clientLender, string tableName, TableFilter filter) {
    _clientLender = clientLender;
    _tableName = tableName;
    _filter = filter;
  }

  public void OnNewObserver(IExcelObserver newObserver, bool isFirstObserver, IStatusObserver statusObserver) {
    statusObserver.OnStatus($"Snaphotting \"{_tableName}\"");
    Refresh(statusObserver);
  }

  public void OnLastObserverRemoved() {
    // Do nothing.
  }

  public void Refresh(IStatusObserver statusObserver) {
    Task.Run(() => PerformFetchTable(statusObserver));
  }

  private void PerformFetchTable(IStatusObserver statusObserver) {
    using var borrowedClient = _clientLender.Borrow();
    var cos = borrowedClient.Value;
    if (cos.Status != null) {
      statusObserver.OnStatus(cos.Status);
      return;
    }

    if (cos.Client == null) {
      return;
    }

    try {
      using var th = cos.Client.Manager.FetchTable(_tableName);
      using var ct = th.ToClientTable();
      // TODO(kosak): Filter the client table here
      var result = Renderer.Render(ct);
      statusObserver.OnNext(result);
    } catch (Exception ex) {
      statusObserver.OnError(ex);
    }
  }
}
