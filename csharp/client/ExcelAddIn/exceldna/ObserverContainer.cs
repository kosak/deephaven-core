using Deephaven.ExcelAddIn.Util;

namespace Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;

/// <summary>
/// This interface supports managing the mutation (adding and removing) from a collection
/// of IExcelObservers.
/// </summary>
public interface IObserverCollection<T> {
  /// <summary>
  /// Adds an observer to the collection.
  /// </summary>
  /// <param name="observer">The observer</param>
  /// <param name="isFirst">True iff this was the first observer that was added.</param>
  void Add(IObserver<T> observer, out bool isFirst);
  /// <summary>
  /// Removes an observer from the collection.
  /// </summary>
  /// <param name="observer">The observer</param>
  /// <param name="wasLast">True iff this was the final observer that was removed
  /// (leaving the collection empty)</param>
  void Remove(IObserver<T> observer, out bool wasLast);
}

/// <summary>
/// This interface supports the operations for communicating status to a collection
/// of IExcelObservers.
/// </summary>
public interface IDataListener<T> {
  /// <summary>
  /// Transmits an exception to the observers.
  /// </summary>
  public void OnErrorAll(Exception error);

  /// <summary>
  /// Transmits a rectangular array of data to the observers.
  /// </summary>
  /// <param name="data"></param>
  public void OnNextAll(T data);
}

/// <summary>
/// This class implements both the above interfaces.
/// </summary>
public sealed class ObserverContainer<T> : IObserverCollection<T>, IDataListener<T> {
  private readonly object _sync = new();
  private readonly HashSet<IObserver<T>> _observers = new();

  public IObserverCollection<T> GetObserverCollection() {
    return this;
  }

  public IDataListener<T> GetDataListener() {
    return this;
  }

  public void Add(IObserver<T> observer, out bool isFirst) {
    lock (_sync) {
      isFirst = _observers.Count == 0;
      _observers.Add(observer);
    }
  }

  public void Remove(IObserver<T> observer, out bool wasLast) {
    lock (_sync) {
      var removed = _observers.Remove(observer);
      wasLast = removed && _observers.Count == 0;
    }
  }

  public void OnNextAll(T result) {
    foreach (var observer in SafeCopyObservers()) {
      observer.OnNext(result);
    }
  }

  public void OnErrorAll(Exception ex) {
    foreach (var observer in SafeCopyObservers()) {
      observer.OnError(ex);
    }
  }

  private IObserver<T>[] SafeCopyObservers() {
    lock (_sync) {
      return _observers.ToArray();
    }
  }
}

public sealed class StatusOrObserverContainer<T> : IObserverCollection<StatusOr<T>>, IDataListener<StatusOr<T>> {
  public void Add(IObserver<StatusOr<T>> observer, out bool isFirst) {
    throw new NotImplementedException();
  }

  public void Remove(IObserver<StatusOr<T>> observer, out bool wasLast) {
    throw new NotImplementedException();
  }

  public void OnErrorAll(Exception error) {
    throw new NotImplementedException();
  }

  public void OnNextAll(StatusOr<T> data) {
    throw new NotImplementedException();
  }
}