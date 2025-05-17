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
