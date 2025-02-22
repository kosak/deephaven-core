using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> : IObserver<T> {
  private readonly SequentialExecutor _executor = new();
  private readonly HashSet<IObserver<T>> _observers = new();

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
    var disp = item is StatusOr sor ? sor.Copy() : null;
    _executor.Run(() => {
      observer.OnNext(item);
      disp?.Dispose();
    });
  }

  public void OnNext(T item) {
    var disp = item is StatusOr sor ? sor.Copy() : null;
    var observers = _observers.ToArray();
    _executor.Run(() => {
      foreach (var observer in observers) {
        observer.OnNext(item);
      }
      disp?.Dispose();
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
