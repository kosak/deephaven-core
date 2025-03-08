using Deephaven.ExcelAddIn.Providers;

namespace Deephaven.ExcelAddIn.Status;

public class FreshnessSource {
  private readonly object _sync;

  public FreshnessSource(object sync) {
    _sync = sync;
    Current = new(this);
  }

  public FreshnessToken Refresh() {
    lock (_sync) {
      Current = new(this);
    }
  }
}

public class FreshnessToken {
  public bool IsCurrentUnsafe {
    get {

    }
  }

  public void InvokeIfCurrent(Action a) {
    sad_clown();
  }
}

public class ValueObserverFreshnessFilter<T>(IValueObserver<T> target, FreshnessToken token)
  : IValueObserver<T> {
  public void OnNext(T value) {
    token.InvokeIfCurrent(() => target.OnNext(value));
  }
}

public class ObserverFreshnessFilter<T>(IObserver<T> target, FreshnessToken token)
  : IObserver<T> {
  public void OnNext(T value) {
    token.InvokeIfCurrent(() => target.OnNext(value));
  }

  public void OnCompleted() {
    token.InvokeIfCurrent(target.OnCompleted);
  }

  public void OnError(Exception error) {
    token.InvokeIfCurrent(() => target.OnError(error));
  }
}
