using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Util;

public class Boxed<T> {
  public T Value;

  public Boxed(T value) {
    Value = value;
  }
}

public sealed class ObserverContainer<T> : IObserver<T> {
  private readonly object _sync = new();
  private readonly SequentialExecutor _executor = new();
  private readonly Dictionary<IObserver<T>, Boxed<bool>> _observers = new();

  public void AddAndNotify(IObserver<T> observer, T value, out bool isFirst) {
    lock (_sync) {
      isFirst = _observers.Count == 0;
      _observers.Add(observer, new Boxed<bool>(true));
      OnNextHelperLocked([observer], value);
    }
  }

  public void OnNext(T item) {
    lock (_sync) {
      var observers = _observers.ToArray();
      OnNextHelperLocked(observers, item);
    }
  }

  private void OnNextHelperLocked(KeyValuePair<IObserver<T>, Boxed<bool>>[] observers, T item) {
    var disp = item is StatusOr sor ? sor.Share() : null;
    _executor.Run(() => {
      // Note: We're on different thread now. _sync is not held inside here.
      foreach (var (observer, enabled) in observers) {
        if (Interlocked.Read(ref enabled.Value)) {
          observer.OnNext(item);
          Interlocked.
        }
      }
      disp?.Dispose();
    });
  }

  public void Remove(IObserver<T> observer, out bool wasLast) {
    lock (_sync) {
      var removed = _observers.Remove(observer);
      wasLast = removed && _observers.Count == 0;
    }
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

  public static void SetState<T>(ref StatusOr<T> dest, StatusOr<T> newValue) {
    Background666.InvokeDispose(dest);
    dest = newValue.Share();
  }
}
