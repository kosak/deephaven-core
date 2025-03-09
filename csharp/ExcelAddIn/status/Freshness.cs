using Deephaven.ExcelAddIn.Providers;

namespace Deephaven.ExcelAddIn.Status;

public class FreshnessTokenSource {
  private readonly object _ownerSync;
  private FreshnessToken _current;

  public FreshnessTokenSource(object ownerSync) {
    _ownerSync = ownerSync;
    _current = new FreshnessToken(this);
  }

  public FreshnessToken Refresh() {
    lock (_ownerSync) {
      _current = new(this);
      return _current;
    }
  }

  public bool IsCurrent(FreshnessToken token) {
    lock (_ownerSync) {
      return token == _current;
    }
  }

  public void InvokeUnderLockIfCurrent(FreshnessToken token, Action a) {
    lock (_ownerSync) {
      if (token != _current) {
        return;
      }
      a();
    }
  }
}

public class FreshnessToken(FreshnessTokenSource owner) {
  public bool IsCurrent => owner.IsCurrent(this);

  public void InvokeUnderLockIfCurrent(Action a) => owner.InvokeUnderLockIfCurrent(this, a);
}

public class ValueObserverFreshnessFilter<T>(IValueObserver<T> target, FreshnessToken token)
  : IValueObserver<T> {
  public void OnNext(T value) {
    token.InvokeUnderLockIfCurrent(() => target.OnNext(value));
  }
}

public class ObserverFreshnessFilter<T>(IObserver<T> target, FreshnessToken token)
  : IObserver<T> {
  public void OnNext(T value) {
    token.InvokeUnderLockIfCurrent(() => target.OnNext(value));
  }

  public void OnCompleted() {
    token.InvokeUnderLockIfCurrent(target.OnCompleted);
  }

  public void OnError(Exception error) {
    token.InvokeUnderLockIfCurrent(() => target.OnError(error));
  }
}
