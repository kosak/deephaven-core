using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Models;

public record EndpointHealth;

public record EndpointId(string Id) {
  public override string ToString() => Id;
}

public record PqName(string Name) {
  public override string ToString() => Name;
}

public record OpStatus(string HumanReadableFunction, RetryKey RetryKey,
  StatusOr<Unit> Status);

public record RetryPlaceholder;
