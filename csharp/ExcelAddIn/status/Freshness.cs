namespace Deephaven.ExcelAddIn.Status;

public class FreshnessSource {
  private readonly object _sync;
  public FreshnessToken Current { get; private set; }

  public FreshnessSource(object sync) {
    _sync = sync;
    Current = new(this);
  }

  public void Reset() {
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
