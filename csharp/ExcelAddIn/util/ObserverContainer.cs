using Deephaven.ExcelAddIn.Status;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> : IObserver<T> {
  private readonly object _sync = new object();
  private readonly SequentialExecutor _executor = new();
  private readonly HashSet<IObserver<T>> _observers = new();
  private readonly InUseTracker<IObserver<T>> _inUseTracker = new();

  public void Add(IObserver<T> observer, out bool isFirst) {
    isFirst = _observers.Count == 0;
    _observers.Add(observer);
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

  public void OnNextOne(IObserver<T> observer, T item) {
    var disp = item is StatusOr sor ? sor.Copy() : null;
    _inUseTracker.AddAll(observer);
    _executor.Run(() => {
      observer.OnNext(item);
      disp?.Dispose();
      _inUseTracker.RemoveAll(observer);
    });
  }

  public void OnNext(T item) {
    var disp = item is StatusOr sor ? sor.Copy() : null;
    var observers = _observers.ToArray();
    _inUseTracker.AddAll(observers);

    _executor.Run(() => {
      foreach (var observer in observers) {
        observer.OnNext(item);
      }
      disp?.Dispose();
      // Probably need a try-finally so this always gets called. ugh
      _inUseTracker.UnMark(observers);
    });
  }

  public void OnError(Exception ex) {
    var observers = _observers.ToArray();
    _inUseTracker.AddAll(observers);
    _executor.Run(() => {
      foreach (var observer in observers) {
        observer.OnError(ex);
      }
      _inUseTracker.UnMark(observers);
    });
  }

  public void OnCompleted() {
    var observers = _observers.ToArray();
    _inUseTracker.AddAll(observers);
    _executor.Run(() => {
      foreach (var observer in observers) {
        observer.OnCompleted();
      }
      _inUseTracker.UnMark(observers);
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
