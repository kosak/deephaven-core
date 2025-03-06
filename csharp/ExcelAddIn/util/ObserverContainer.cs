using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Util;

public sealed class ObserverContainer<T> : IStatusObserver<T> {
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

  public void Remove(IStatusObserver<T> observer, out bool wasLast) {
    lock (_sync) {
      var removed = _observers.Remove(observer);
      wasLast = removed && _observers.Count == 0;
    }
  }

  public void OnNext(T item) {
    lock (_sync) {
      OnNextHelperLocked(_observers, item);
    }
  }


  public void OnStatus(string status) {
    lock (_sync) {
      foreach (var observer in _observers) {
        _executor.Run(() => observer.OnStatus(status));
      }
    }
  }

  private void OnNextHelperLocked(IEnumerable<IStatusObserver<T>> observers, T item) {
    foreach (var observer in observers) {
      _executor.Run(() => observer.OnNext(item));
    }
    // If the item is RefCounted, then before leaving, acquire it here
    // and then release it in the executor.
    if (item is RefCounted rc) {
      var disp = rc.Share();
      _executor.Run(disp.Dispose);
    }
  }
}

public static class RefUtil {
  public static void SetStatusAndNotify<T>(ref StatusOr<RefCounted<T>> dest,
    string status, ObserverContainer<RefCounted<T>> container) where T : class, IDisposable {
    Background.InvokeDispose(dest);
    container.OnStatus(status);
  }

  public static void SetStateAndNotify<T>(ref StatusOr<RefCounted<T>> dest,
    StatusOr<RefCounted<T>> newValue,
    ObserverContainer<T> container) where T : class, IDisposable {
    Background.InvokeDispose(dest);
    container.OnNext(newValue);
  }


  public static void SetState<T>(ref StatusOr<T> dest, StatusOr<T> newValue) {
    Background666.InvokeDispose(dest);
    dest = newValue.Share();
  }
}
