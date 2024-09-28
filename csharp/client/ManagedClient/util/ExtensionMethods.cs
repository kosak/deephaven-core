namespace Deephaven.ManagedClient;

public static class ExtensionMethods {
  public static int ToIntExact(this long value) {
    var result = (int)value;
    if (result != value) {
      throw new ArithmeticException($"{value} does not fit in an int");
    }

    return result;
  }
}
