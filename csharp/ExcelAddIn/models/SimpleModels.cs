namespace Deephaven.ExcelAddIn.Models;

public record EndpointHealth;

public record EndpointId(string Id) {
  public override string ToString() => Id;
}

public record PqName(string Name) {
  public override string ToString() => Name;
}
