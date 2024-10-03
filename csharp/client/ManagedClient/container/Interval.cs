namespace Deephaven.ManagedClient;

public readonly record struct Interval(UInt64 Begin, UInt64 End) {
  public static readonly Interval OfEmpty = new(0, 0);

  public static Interval OfSingleton(UInt64 value) => new (value, checked(value + 1));
  public static Interval Of(UInt64 begin, UInt64 end) => new(begin, end);

  public UInt64 Count => End - Begin;
  public bool IsEmpty => Begin == End;
}
