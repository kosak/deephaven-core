namespace Deephaven.Dh_NetClient;

public struct InterlockedLong {
  private long _value;

  public long Read() {
    return Interlocked.Read(ref _value);
  }

  public long Increment() {
    return Interlocked.Increment(ref _value);
  }
}
