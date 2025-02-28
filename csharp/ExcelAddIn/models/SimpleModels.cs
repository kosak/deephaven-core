namespace Deephaven.ExcelAddIn.Models;

/**
 * Strong type for EndpointId
 */
public readonly record struct EndpointId(string Id) {
  public override string ToString() => Id;
}

public record EndpointHealth;
