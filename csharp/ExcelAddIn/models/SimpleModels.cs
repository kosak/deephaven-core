namespace Deephaven.ExcelAddIn.Models;

public record AddOrRemove<T>(bool IsAdd, T Value) {
  public static AddOrRemove<T> OfAdd(T value) {
    return new AddOrRemove<T>(true, value);
  }

  public static AddOrRemove<T> OfRemove(T value) {
    return new AddOrRemove<T>(false, value);
  }
}

/**
 * Strong type for EndpointId
 */
public record EndpointId(string Id) {
  public override string ToString() => Id;
}

/**
 * Strong type for PersistentQueryName
 */
public record PersistentQueryName(string Name) {
  public override string ToString() => Name;
}

public record EndpointHealth;
