namespace Deephaven.ManagedClient;

public struct InterlockedLong {
  private long _value;

  public long Read() {
    return Interlocked.Read(ref _value);
  }
}
