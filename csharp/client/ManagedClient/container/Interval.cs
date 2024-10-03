namespace Deephaven.ManagedClient;

public record struct Interval(UInt64 Begin, UInt64 End) {
  public static Interval Empty = new(0, 0);
  public UInt64 Count => End - Begin;
}
