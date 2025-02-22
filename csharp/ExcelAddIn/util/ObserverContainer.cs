using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> : IObserver<T> {
  private readonly SequentialExecutor _executor;
  private readonly HashSet<IObserver<T>> _observers = new();

  public ObserverContainer(SequentialExecutor executor) {
    _executor = executor;
  }

  public int Count => _observers.Count;

  public void Add(IObserver<T> observer, out bool isFirst) {
    isFirst = _observers.Count == 0;
    _observers.Add(observer);
  }

  public void Remove(IObserver<T> observer, out bool wasLast) {
    var removed = _observers.Remove(observer);
    wasLast = removed && _observers.Count == 0;
  }

  public void OnNextOne(IObserver<T> observer, T item, IDisposable? onExit = null) {
    _executor.Enqueue(() => {
      observer.OnNext(item);
      onExit?.Dispose();
    });
  }

  public void OnNext(T result) {
    var observers = _observers.ToArray();
    var kept = KeepAlive.TryReference(result);
    _executor.Enqueue(() => {
      foreach (var observer in observers) {
        observer.OnNext(result);
      }
      kept?.Dispose();
    });
  }

  public void OnError(Exception ex) {
    var observers = _observers.ToArray();
    _executor.Enqueue(() => {
      foreach (var observer in observers) {
        observer.OnError(ex);
      }
    });
  }

  public void OnCompleted() {
    var observers = _observers.ToArray();
    _executor.Enqueue(() => {
      foreach (var observer in observers) {
        observer.OnCompleted();
      }
    });
  }
}
