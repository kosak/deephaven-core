namespace Deephaven.ExcelAddIn.Status;

/// <summary>
/// Provides a variant of <c>IObserver&lt;T&gt;</c> that also takes a
/// <c>CancellationToken</c>
/// </summary>
public interface IObserverWithCancel<in T> {
  public void OnNext(T value, CancellationToken token);
  public void OnCompleted(CancellationToken token);
  public void OnError(Exception error, CancellationToken token);
}

/// <summary>
/// This (non-generic) class exists to provide a static Create method with type inference.
/// </summary>
public class ObserverWithCancelWrapper {
  public static IObserver<T> Create<T>(IObserverWithCancel<T> wrapped,
    CancellationToken token) {
    return new ObserverWithCancelWrapper<T>(wrapped, token);
  }
}

/// <summary>
/// Adapts an <c>IObserverWithCancel&lt;T&gt;</c> object to an <c>IObserver&lt;T&gt;</c>
/// object by making a closure for a <c>CancellationToken</c>.
/// </summary>
public sealed class ObserverWithCancelWrapper<T>(IObserverWithCancel<T> wrapped,
  CancellationToken token) : IObserver<T> {

  public void OnNext(T value) {
    wrapped.OnNext(value, token);
  }

  public void OnCompleted() {
    wrapped.OnCompleted(token);
  }

  public void OnError(Exception error) {
    wrapped.OnError(error, token);
  }
}
