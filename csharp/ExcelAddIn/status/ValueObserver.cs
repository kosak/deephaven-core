namespace Deephaven.ExcelAddIn.Status;

/// <summary>
/// Provides a simplified IObserver-style interface. Normal IObservers can signal
/// termination either by OnCompleted (for normal termination) or OnError (for abnormal
/// termination). In the Excel context, all of our observers are perpetual until
/// unsubscribed, so the OnCompleted and OnError implementations were just adding
/// code noise. Our IValueObservers only provide <see cref="OnNext"/>. In our model,
/// errors are transitory rather than fatal (i.e. they can be followed by non-errors).
/// If an obsever needs to support a transitory error, they can make it part of their
/// value type. We commonly do this by using the StatusOr&lt;T&gt; type.
/// </summary>
public interface IValueObserver<in T> {
  public void OnNext(T value);
}

public interface IValueObservable<out T> {
  IDisposable Subscribe(IValueObserver<T> observer);
}

/// <summary>
/// Provides a variant of <c>IValueObserver&lt;T&gt;</c> that also takes a
/// <c>CancellationToken</c>
/// </summary>
public interface IValueObserverWithCancel<in T> {
  public void OnNext(T value, CancellationToken token);
}

/// <summary>
/// This (non-generic) class exists to provide a static Create method with type inference.
/// </summary>
public class ValueObserverWithCancelWrapper {
  public static IValueObserver<T> Create<T>(IValueObserverWithCancel<T> wrapped,
    CancellationToken token) {
    return new ValueObserverWithCancelWrapper<T>(wrapped, token);
  }
}

/// <summary>
/// Adapts an <c>IValueObserverWithCancel&lt;T&gt;</c> object to an <c>IValueObserver&lt;T&gt;</c>
/// object by making a closure for a <c>CancellationToken</c>.
/// </summary>
public class ValueObserverWithCancelWrapper<T>(IValueObserverWithCancel<T> wrapped,
  CancellationToken token) : IValueObserver<T> {

  public void OnNext(T value) {
    wrapped.OnNext(value, token);
  }
}
