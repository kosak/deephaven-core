namespace Deephaven.ExcelAddIn.Status;

public class FreshnessSource {
  private readonly object _sync;
  public FreshnessToken Current { get; private set; }

  public FreshnessSource(object sync) {
    _sync = sync;
    Current = new();
  }

  public void New() {
    lock (_sync) {
      Current = new FreshnessToken(this);
    }
  }
}

public class FreshnessToken {
  public bool IsCurrentUnsafe {
    get {

    }
  }
}
