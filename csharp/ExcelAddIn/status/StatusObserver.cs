using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

public interface IStatusObserver<in T> {
  public void OnStatus(string status);
  public void OnNext(T value);
}

public interface IStatusObservable<out T> {
  void Subscribe(IStatusObserver<T> observer);
}
