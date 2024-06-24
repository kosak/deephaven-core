using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal sealed class DeephavenHandler : IExcelObservable {
  private readonly TableOperationManager _tableOperationManager;
  private readonly IDeephavenTableOperation _tableOperation;
  private readonly object _sync = new();
  private readonly HashSet<IExcelObserver> _observers = new();

  public DeephavenHandler(TableOperationManager tableOperationManager, IDeephavenTableOperation tableOperation) {
    _tableOperationManager = tableOperationManager;
    _tableOperation = tableOperation;
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    bool isFirstObserver;
    lock (_sync) {
      isFirstObserver = _observers.Count == 0;
      _observers.Add(observer);
    }

    if (isFirstObserver) {
      _tableOperationManager.Register(_tableOperation);
    }

    return new ActionDisposable(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IExcelObserver observer) {
    lock (_sync) {
      _observers.Remove(observer);
      if (_observers.Count != 0) {
        return;
      }
    }

    _tableOperationManager.Unregister(_tableOperation);
  }

  private IStatusObserver MakeStatusObserver() {
    lock (_sync) {
      return new SuperNubbin666(_observers.ToArray());
    }
  }
}

public interface IStatusObserver {
  public void OnStatus(string message) {
    var matrix = new object[1, 1];
    matrix[0, 0] = message;
    OnNext(matrix);
  }

  public void OnNext(object?[,] result);

  public void OnError(Exception error) {
    OnStatus(error.Message);
  }
}

public interface IDeephavenTableOperation {
  void Start(ClientOrStatus clientOrStatus, IStatusObserver statusObserver);
  void Stop();
}
