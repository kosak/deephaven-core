namespace Deephaven.ManagedClient;

public readonly struct LocalTime {
  public readonly Int64 Nanos;

  public static LocalTime FromNanos(Int64 nanos) => new(nanos);

  public LocalTime(Int64 nanos) {
    Nanos = nanos == DeephavenConstants.NullLong ? 0 : nanos;
  }

  public override string ToString() {
    return $"[Nanos={Nanos}]";
  }
}
