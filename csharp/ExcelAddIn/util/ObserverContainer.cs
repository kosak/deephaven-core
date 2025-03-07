using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using System;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> : IStatusObserver<T> {
  private readonly object _sync = new();
  private readonly SequentialExecutor _executor = new();
  private readonly HashSet<IStatusObserver<T>> _observers = new();

  public void AddAndNotify(IStatusObserver<T> observer, T value, out bool isFirst) {
    lock (_sync) {
      isFirst = _observers.Count == 0;
      _observers.Add(observer);
      _executor.Run(() => observer.OnNext(value));
    }
  }

  public void Remove(IStatusObserver<T> observer, out bool wasLast) {
    lock (_sync) {
      var removed = _observers.Remove(observer);
      wasLast = removed && _observers.Count == 0;
    }
  }

  public void OnNext(T item) {
    lock (_sync) {
      foreach (var observer in _observers) {
        _executor.Run(() => observer.OnNext(item));
      }
    }
  }

  public void OnStatus(string status) {
    lock (_sync) {
      foreach (var observer in _observers) {
        _executor.Run(() => observer.OnStatus(status));
      }
    }
  }
}
