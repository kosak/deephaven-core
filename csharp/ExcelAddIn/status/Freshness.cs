﻿namespace Deephaven.ExcelAddIn.Status;

public class FreshnessSource {
  private readonly object _sync;
  public FreshnessToken Current { get; private set; }

  public FreshnessSource(object sync) {
    _sync = sync;
    Current = new();
  }

  public FreshnessToken New() {
    _currentToken = new FreshnessToken(this);
    return _currentToken;
  }
}

public class FreshnessToken {
  public bool IsCurrentUnsafe {
    get {

    }
  }
}
