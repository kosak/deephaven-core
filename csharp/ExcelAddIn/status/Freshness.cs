using Deephaven.ExcelAddIn.Providers;

namespace Deephaven.ExcelAddIn.Status;

public class FreshnessSource {
  private readonly object _sync;
  public FreshnessToken Current { get; private set; }

  public FreshnessSource(object sync) {
    _sync = sync;
    Current = new(this);
  }

  public void Refresh() {
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
}

public class FreshnessFilter<T>(IValueObserver<T> target, FreshnessToken token)
  : IValueObserver<T> {
  public void OnNext(T value) {
    token.InvokeIfCurrent(() => target.OnNext(value));
  }
}
