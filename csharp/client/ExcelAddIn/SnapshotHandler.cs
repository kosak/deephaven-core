using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal abstract class DeephavenHandler : IExcelObservable, IObserver<bool> {
  protected readonly Lender _clientLender;
  private IDisposable? _clientObserverDisposer = null;
  private readonly object _sync = new();
  private readonly HashSet<IExcelObserver> _observers = new();

  protected DeephavenHandler(Lender clientLender) =>
    _clientLender = clientLender;

  public IDisposable Subscribe(IExcelObserver observer) {
    bool isFirstObserver;
    lock (_sync) {
      isFirstObserver = _observers.Count == 0;
      _observers.Add(observer);
    }

    // If the first observer, listen for client changes (e.g. reconnect button).
    if (isFirstObserver) {
      _clientObserverDisposer = _clientLender.Subscribe(this);
    }

    // if susbscribing, new observer should just wait for the next message
    // if snapshotting, everyone should probably do a redo
    // if first time, both need to do something
    // ugh
    OnNewObserver(observer, isFirstObserver);
    return new ActionDisposable(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IExcelObserver observer) {
    lock (_sync) {
      _observers.Remove(observer);
      if (_observers.Count != 0) {
        return;
      }
    }

    // No more observers. Stop observing client changes.
    var temp = _clientObserverDisposer;
    _clientObserverDisposer = null;
    temp!.Dispose();

    OnLastObserverRemoved();
  }

  private protected void PublishStatusMessage(string message) {
    var matrix = new object[1, 1];
    matrix[0, 0] = message;
    PublishResult(matrix);
  }

  private protected void PublishException(Exception ex) {
    PublishStatusMessage(ex.Message);
  }

  private protected void PublishResult(object?[,] matrix) {
    IExcelObserver[] observers;
    lock (_sync) {
      observers = _observers.ToArray();
    }

    foreach (var observer in observers) {
      observer.OnNext(matrix);
    }
  }

  void IObserver<bool>.OnCompleted() {
    // Do nothing (for now)
  }

  void IObserver<bool>.OnError(Exception error) {
    // Do nothing (for now)
  }

  void IObserver<bool>.OnNext(bool ignored) {
    Refresh();
  }

  private protected abstract void Refresh();
  private protected abstract void OnNewObserver(IExcelObserver observer, bool isFirstObserver);
  private protected abstract void OnLastObserverRemoved();
}

internal class SnapshotHandler : DeephavenHandler {
  private readonly string _tableName;
  private readonly TableFilter _filter;

  public SnapshotHandler(Lender<Client> clientLender, string tableName, TableFilter filter) : base(clientLender) {
    _tableName = tableName;
    _filter = filter;
  }

  private protected override void Refresh() {
    Task.Run(PerformFetchTable);
  }

  private protected override void OnNewObserver(IExcelObserver newObserver, bool isFirstObserver) {
    PublishStatusMessage($"Snaphotting \"{_tableName}\"");
    Refresh();
  }

  private protected override void OnLastObserverRemoved() {
    // Do nothing.
  }

  private void PerformFetchTable() {
    using var borrowedClient = _clientLender.Borrow();
    var client = borrowedClient.Value;
    if (client == null) {
      PublishStatusMessage("Not connected to Deephaven.");
      return;
    }

    try {
      using var th = client.Manager.FetchTable(_tableName);
      using var ct = th.ToClientTable();
      // TODO(kosak): Filter the client table here
      var result = Renderer.Render(ct);
      PublishResult(result);
    } catch (Exception ex) {
      PublishException(ex);
    }
  }
}
