﻿using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> {
  private readonly object _sync = new();
  private readonly SequentialExecutor _executor = new();
  private readonly HashSet<IStatusObserver<T>> _observers = new();

  public void AddAndNotify(IStatusObserver<T> observer, T value, out bool isFirst) {
    lock (_sync) {
      isFirst = _observers.Count == 0;
      _observers.Add(observer);
      OnNextHelperLocked([observer], value);
    }
  }

  public void OnNext(T item) {
    lock (_sync) {
      var observers = _observers.ToArray();
      OnNextHelperLocked(observers, item);
    }
  }

  private void OnNextHelperLocked(IStatusObserver<T>[] observers, T item) {
    var disp = item is StatusOr sor ? sor.Share() : null;
    _executor.Run(() => {
      // Note: We're on different thread now. _sync is not held inside here.
      foreach (var observer in observers) {
        observer.OnNext(item);
      }
      disp?.Dispose();
    });
  }

  public void Remove(IStatusObserver<T> observer, out bool wasLast) {
    lock (_sync) {
      var removed = _observers.Remove(observer);
      wasLast = removed && _observers.Count == 0;
    }
  }
}

public static class ProviderUtil {
  public static void SetStateAndNotify<T>(ref StatusOr<T> dest, StatusOr<T> newValue,
    ObserverContainer<StatusOr<T>> container) {
    Background666.InvokeDispose(dest);
    dest = newValue.Share();
    container.OnNext(newValue);
  }

  public static void SetState<T>(ref StatusOr<T> dest, StatusOr<T> newValue) {
    Background666.InvokeDispose(dest);
    dest = newValue.Share();
  }
}
