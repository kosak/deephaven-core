using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;

/// <summary>
/// Operations (e.g. SnapshotOperation or SubscribeOperation) are designed to support
/// multiple Excel "observers". This interface describes the add/remove operations
/// for a collection of IExcelObservers.
/// </summary>
public interface IObserverCollectionManager {
  void Add(IExcelObserver observer, out bool isFirst);
  void Remove(IExcelObserver observer, out bool wasLast);
}

/// <summary>
/// This interface supports the operations for communicating status to a collection
/// of IExcelObservers.
/// </summary>
public interface IObserverCollectionSender {
  public void OnStatus(string message);
  public void OnError(Exception error);
  public void OnNext(object?[,] result);
}

/// <summary>
/// This class implements both the IObserverCollectionManager and IObserverCollectionSender
/// roles.
/// </summary>
public class ObserverContainer : IObserverCollectionManager, IObserverCollectionSender {
  private readonly object _sync = new();
  private readonly HashSet<IExcelObserver> _observers = new();

  public void Add(IExcelObserver observer, out bool isFirst) {
    lock (_sync) {
      isFirst = _observers.Count == 0;
      _observers.Add(observer);
    }
  }

  public void Remove(IExcelObserver observer, out bool wasLast) {
    lock (_sync) {
      _observers.Remove(observer);
      wasLast = _observers.Count == 0;
    }
  }

  public void OnStatus(string message) {
    var matrix = new object[1, 1];
    matrix[0, 0] = message;
    OnNext(matrix);
  }

  public void OnError(Exception error) {
    OnStatus(error.Message);
  }

  public void OnNext(object?[,] result) {
    IExcelObserver[] observers;
    lock (_sync) {
      observers = _observers.ToArray();
    }

    foreach (var observer in observers) {
      observer.OnNext(result);
    }
  }
}
