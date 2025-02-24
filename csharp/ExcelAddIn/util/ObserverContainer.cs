﻿using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> : IObserver<T> {
  private readonly object _sync = new();
  private readonly SequentialExecutor _executor = new();
  private readonly HashSet<IObserver<T>> _observers = new();

  public void AddAndNotify(IObserver<T> observer, T value, out bool isFirst) {
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

  private void OnNextHelperLocked(IObserver<T>[] observers, T item) {
    var disp = item is StatusOr sor ? sor.Share() : null;
    _executor.Run(() => {
      // Note: We're on different thread now. _sync is not held inside here.
      foreach (var observer in observers) {
        observer.OnNext(item);
      }
      disp?.Dispose();
    });
  }

  public void RemoveAndWait(IObserver<T> observer, out bool wasLast) {
    Task task;
    lock (_sync) {
      var removed = _observers.Remove(observer);
      wasLast = removed && _observers.Count == 0;
      if (!removed) {
        return;
      }
      // This little job will set the task flag at the end of the
      // SequentialExecutor queue. By waiting until the SequentialExecutor
      // gets to this point, we can prove that we will not have any more
      // references to "observer".
      var tcs = new TaskCompletionSource();
      task = tcs.Task;
      _executor.Run(tcs.SetResult);
    }

    task.Wait();
  }

  public void OnError(Exception ex) {
    lock (_sync) {
      var observers = _observers.ToArray();
      _executor.Run(() => {
        // Note: We're on different thread now. _sync is not held inside here.
        foreach (var observer in observers) {
          observer.OnError(ex);
        }
      });
    }
  }

  public void OnCompleted() {
    lock (_sync) {
      var observers = _observers.ToArray();
      _executor.Run(() => {
        // Note: We're on different thread now. _sync is not held inside here.
        foreach (var observer in observers) {
          observer.OnCompleted();
        }
      });
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
}
