namespace Deephaven.ExcelAddIn.Providers;

public interface IStatusObserver<in T> {
  public void OnStatus(string status);
  public void OnNext(T value);
}

public interface IStatusObserverWithCookie<in T> {
  public void OnStatus(string status, object cookie);
  public void OnNext(T value, object cookie);
}

public interface IStatusObservable<out T> {
  IDisposable Subscribe(IStatusObserver<T> observer);
}
