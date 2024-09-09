namespace Deephaven.ExcelAddIn.Util;

public class VersionTracker {
  private readonly object _sync = new();
  private Cookie _cookie;

  public VersionTracker() {
    _cookie = new Cookie(this);
  }

  public Cookie New() {
    lock (_sync) {
      _cookie = new Cookie(this);
      return _cookie;
    }
  }

  public class Cookie(VersionTracker owner) {
    public bool IsCurrent {
      get {
        lock (owner._sync) {
          return ReferenceEquals(owner._cookie, this);
        }
      }
    }
  }
}
