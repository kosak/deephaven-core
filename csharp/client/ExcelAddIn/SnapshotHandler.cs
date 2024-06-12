using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal abstract class DeephavenHandler : IExcelObservable {
  protected readonly IClientProvider _clientProvider;
  private readonly object _sync = new();
  private readonly HashSet<IExcelObserver> _observers = new();

  public IDisposable Subscribe(IExcelObserver observer) {
    bool isFirstObserver;
    lock (_sync) {
      isFirstObserver = _observers.Count == 0;
      _observers.Add(observer);
    }
    // if susbscribing, new observer should just wait for the next message
    // if snapshotting, everyone should probably do a redo
    // if first time, both need to do something
    // ugh
    OnNewObserver(observer, isFirstObserver);
    return new ActionDisposable(() => RemoveObserver(observer));
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

  private void RemoveObserver(IExcelObserver observer) {
    bool isLastObserver;
    lock (_sync) {
      _observers.Remove(observer);
      isLastObserver = _observers.Count == 0;
    }

    if (isLastObserver) {
      OnLastObserverRemoved();
    }
  }

  private protected abstract void OnClientChange();
  private protected abstract void OnNewObserver(IExcelObserver observer, bool isFirstObserver);
  private protected abstract void OnLastObserverRemoved();
}

internal class SnapshotHandler : DeephavenHandler {
  private readonly string _tableName;

  private protected override void OnClientChange() {
    Doit();
  }

  private protected override void OnNewObserver(IExcelObserver newObserver, bool isFirstObserver) {
    PublishStatusMessage($"Snaphotting \"{_tableName}\"");
    Doit();
  }

  private void Doit() {
    Task.Run(PerformFetchTable);
  }

  private void PerformFetchTable() {
    if (!_clientProvider.TryGetClient(out var client)) {
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
