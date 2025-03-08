using Deephaven.ExcelAddIn.Providers;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> : IValueObserver<T> {
  private readonly SequentialExecutor _executor = new();
  private readonly HashSet<IValueObserver<T>> _observers = new();

  public void AddAndNotify(IValueObserver<T> observer, T value, out bool isFirst) {
    isFirst = _observers.Count == 0;
    _observers.Add(observer);
    _executor.Run(() => observer.OnNext(value));
  }

  public void Remove(IValueObserver<T> observer, out bool wasLast) {
    var removed = _observers.Remove(observer);
    wasLast = removed && _observers.Count == 0;
  }

  public void OnNext(T item) {
    foreach (var observer in _observers) {
      _executor.Run(() => observer.OnNext(item));
    }
  }

  public void EnqueueAction(Action action) {
    _executor.Run(action);
  }
}
