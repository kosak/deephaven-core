using System.Diagnostics;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> : IObserver<T> {
  private readonly HashSet<IObserver<T>> _observers = new();
  private readonly SequentialExecutor _executor = new();

  public int Count => _observers.Count;

  public void Add(IObserver<T> observer, out bool isFirst) {
    isFirst = _observers.Count == 0;
    _observers.Add(observer);
  }

  public void Remove(IObserver<T> observer, out bool wasLast) {
    var removed = _observers.Remove(observer);
    wasLast = removed && _observers.Count == 0;
  }

  public void OnNext(T result) {
    OnNext(result, null);
  }

  public void OnNext(T result, IDisposable? onExit) {
    var observers = _observers.ToArray();
    _executor.Enqueue(() => {
      foreach (var observer in observers) {
        observer.OnNext(result);
      }
      onExit?.Dispose();
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
