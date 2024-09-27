namespace Deephaven.ManagedClient;

public readonly struct LocalDate {
  public readonly Int64 Millis;

  public static LocalDate FromMillis(Int64 millis) => new(millis);

  public LocalDate(Int64 millis) {
    Millis = millis == DeephavenConstants.NullLong ? 0 : millis;
  }

  public override string ToString() {
    return $"[Millis={Millis}]";
  }
}
