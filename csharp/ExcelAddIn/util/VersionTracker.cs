namespace Deephaven.ExcelAddIn.Util;

internal class VersionTracker {
  private VersionTrackerCookie _cookie;

  public VersionTracker() {
    _cookie = new VersionTrackerCookie(this);
  }

  public VersionTrackerCookie SetNewVersion() {
    _cookie = new VersionTrackerCookie(this);
    return _cookie;
  }

  public bool HasCookie(VersionTrackerCookie cookie) {
    return ReferenceEquals(_cookie, cookie);
  }
}

internal class VersionTrackerCookie(VersionTracker owner) {
  public bool IsCurrent => owner.HasCookie(this);
}
