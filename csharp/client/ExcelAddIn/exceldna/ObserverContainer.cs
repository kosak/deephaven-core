using Deephaven.ExcelAddIn.Util;

namespace Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;

/// <summary>
/// This interface supports managing the mutation (adding and removing) from a collection
/// of IExcelObservers.
/// </summary>
public interface IObserverCollection<out T> {
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
/// of IObserver&lt;T&gt;
/// </summary>
public interface IDataListener<in T> {
  public void OnErrorAll(Exception error);
  public void OnNextAll(T data);
  public void OnCompletedAll();
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

  public void OnCompletedAll() {
    foreach (var observer in SafeCopyObservers()) {
      observer.OnCompleted();
    }
  }

  private IObserver<T>[] SafeCopyObservers() {
    lock (_sync) {
      return _observers.ToArray();
    }
  }
}
