using Deephaven.ExcelAddIn.Status;
using Deephaven.ManagedClient;
using System;

namespace Deephaven.ExcelAddIn.Util;

public class InUseTracker<T> {
  public void MarkAll(IEnumerable<T> items) {

  }

  public void ReleaseAll(IEnumerable<T> items) {

  }


}

public sealed class ObserverContainer<T> : IObserver<T> {
  private readonly object _sync = new object();
  private readonly SequentialExecutor _executor = new();
  private readonly HashSet<IObserver<T>> _observers = new();
  private readonly InUseTracker<IObserver<T>> _inUseTracker = new();

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
    _inUseTracker.MarkAll(observers);
    _executor.Run(() => {
      // Note: We're on different thread now. _sync is not held inside here.
      foreach (var observer in observers) {
        observer.OnNext(item);
      }
      // Probably need a try-finally so this always gets called. ugh
      _inUseTracker.ReleaseAll(observers);
      disp?.Dispose();
    });
  }


  /**
   * This is probably a mistake... wait???? under lock?? really?
   * I need some way to guarantee that an observe won't be called after my caller
   * calls remove
   */
  public void RemoveAndWait666(IObserver<T> observer, out bool wasLast) {
    var removed = _observers.Remove(observer);
    wasLast = removed && _observers.Count == 0;
    _inUseTracker.WaitUntilNotInUse(observer);
  }


  public void OnError(Exception ex) {
    void Action() {
      foreach (var observer in observers) {
        observer.OnError(ex);
      }
      // Probably need a try-finally so this always gets called. ugh
      _inUseTracker.ReleaseAll(observers);
    }

    lock (_sync) {
      _inUseTracker.MarkAll(observers);
      _executor.Run(Action);
    }

    lock (_sync) {
      var observers = GetObservers();
      _inUseTracker.MarkAll(observers);
      _executor.Run(() => {
        foreach (var observer in observers) {
          observer.OnError(ex);
        }
        _inUseTracker.UnMark(observers);
      });
    }
  }

  public void OnCompleted() {
    var observers = GetObservers();
    _inUseTracker.MarkAll(observers);
    _executor.Run(() => {
      foreach (var observer in observers) {
        observer.OnCompleted();
      }
      _inUseTracker.UnmarkAll(observers);
    });
  }
}

public static class ObserverContainer_Extensions {
  public static void SetStateAndNotify<T>(this ObserverContainer<StatusOr<T>> container,
    ref StatusOr<T> dest, StatusOr<T> newValue) {
    Background666.InvokeDispose(dest);
    dest = newValue.Share();
    container.OnNext(newValue);
  }
}
