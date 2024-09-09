using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Observable;

/// <summary>
/// Our "ValueObservable" is similar to the standard Observable interface except:
/// <ul>
/// <li>We accept subscriptions from IValueObservers</li>
/// <li>Along with IDisposable, our return type also includes a best-effort Retry method</li>
/// </ul>
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IValueObservable<out T> {
  IObservableCallbacks Subscribe(IValueObserver<T> observer);
}

public interface IObservableCallbacks : IDisposable {
  void Retry();
}

internal class ObservableCallbacks : IObservableCallbacks {
  public static IObservableCallbacks Create(Action retryAction, Action disposeAction) {
    return new ObservableCallbacks(retryAction, disposeAction);
  }

  private readonly Action _retryAction;
  private Action? _disposeAction;

  private ObservableCallbacks(Action retryAction, Action disposeAction) {
    _retryAction = retryAction;
    _disposeAction = disposeAction;
  }

  public void Retry() {
    _retryAction();
  }

  public void Dispose() {
    Utility.Exchange(ref _disposeAction, null)?.Invoke();
  }
}
