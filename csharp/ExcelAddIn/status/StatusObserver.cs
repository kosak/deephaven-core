namespace Deephaven.ExcelAddIn.Providers;

public interface IStatusObserver<in T> {
  public void OnStatus(string status);
  public void OnNext(T value);
}

public interface IStatusObservable<out T> {
  IDisposable Subscribe(IStatusObserver<T> observer);
}
