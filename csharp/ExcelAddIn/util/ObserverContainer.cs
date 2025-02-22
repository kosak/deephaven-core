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

  public void OnNextOne(IObserver<T> observer, T item) {
    var kept = KeepAlive.TryReference(item);
    _executor.Run(() => {
      observer.OnNext(item);
      kept?.Dispose();
    });
  }

  public void OnNext(T item) {
    var observers = _observers.ToArray();
    var kept = KeepAlive.TryReference(item);
    _executor.Run(() => {
      foreach (var observer in observers) {
        observer.OnNext(item);
      }
      kept?.Dispose();
    });
  }

  public void OnError(Exception ex) {
    var observers = _observers.ToArray();
    _executor.Run(() => {
      foreach (var observer in observers) {
        observer.OnError(ex);
      }
    });
  }

  public void OnCompleted() {
    var observers = _observers.ToArray();
    _executor.Run(() => {
      foreach (var observer in observers) {
        observer.OnCompleted();
      }
    });
  }
}
