namespace Deephaven.ManagedClient;

public static class ExtensionMethods {
  public static int ToIntExact(this long value) {
    return checked((int)value);
  }

  public static int ToIntExact(this ulong value) {
    return checked((int)value);
  }
}
