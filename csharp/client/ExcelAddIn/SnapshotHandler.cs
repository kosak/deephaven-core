using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal abstract class DeephavenHandler : IExcelObservable, IObserver<Client> {
  private readonly IObservable<Client> _clientObservable;
  private IDisposable? _clientObserverDisposer = null;
  private readonly object _sync = new();
  private Client? _client = null;
  private readonly HashSet<IExcelObserver> _observers = new();

  protected DeephavenHandler(IObservable<Client> clientObservable) =>
    _clientObservable = clientObservable;

  public IDisposable Subscribe(IExcelObserver observer) {
    bool isFirstObserver;
    lock (_sync) {
      isFirstObserver = _observers.Count == 0;
      _observers.Add(observer);
    }

    // If the first observer, listen for client changes (e.g. reconnect button).
    if (isFirstObserver) {
      _clientObserverDisposer = _clientObservable.Subscribe(this);
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
    foreach (var observer in GetObservers()) {
      observer.OnNext(matrix);
    }
  }

  private IExcelObserver[] GetObservers() {
    lock (_sync) {
      return _observers.ToArray();
    }
  }

  protected Client? GetClient() {
    lock (_sync) {
      return _client;
    }
  }

  void IObserver<Client>.OnCompleted() {
    // Do nothing (for now)
  }

  void IObserver<Client>.OnError(Exception error) {
    // Do nothing (for now)
  }

  void IObserver<Client>.OnNext(Client value) {
    lock (_sync) {
      _client = value;
    }
    Refresh();
  }

  private protected abstract void Refresh();
  private protected abstract void OnNewObserver(IExcelObserver observer, bool isFirstObserver);
  private protected abstract void OnLastObserverRemoved();
}

internal class SnapshotHandler : DeephavenHandler {
  private readonly string _tableName;
  private readonly TableFilter _filter;

  public SnapshotHandler(IObservable<Client> clientObservable, string tableName, TableFilter filter) : base(clientObservable) {
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
    var client = GetClient();
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
