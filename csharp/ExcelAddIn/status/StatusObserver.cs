namespace Deephaven.ExcelAddIn.Providers;

public interface IValueObserver<in T> {
  public void OnNext(T value);
}

public interface IValueObserverWithCancel<in T> {
  public void OnNext(T value, CancellationToken token);
}

public interface IValueObservable<out T> {
  IDisposable Subscribe(IValueObserver<T> observer);
}

public class ValueObserverWithCancelWrapper {
  public static IValueObserver<T> Create<T>(IValueObserverWithCancel<T> wrapped,
    CancellationToken token) {
    return new ValueObserverWithCancelWrapper<T>(wrapped, token);
  }
}

public class ValueObserverWithCancelWrapper<T>(IValueObserverWithCancel<T> wrapped,
  CancellationToken token) : IValueObserver<T> {

  public void OnNext(T value) {
    wrapped.OnNext(value, token);
  }
}
