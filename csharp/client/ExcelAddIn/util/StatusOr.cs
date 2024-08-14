using System.Diagnostics.CodeAnalysis;

namespace Deephaven.ExcelAddIn.Util;

public class StatusOr<T> {
  public readonly string? Status;
  public readonly T? Value;

  public static StatusOr<T> OfStatus(string status) {

  }

  public static StatusOr<T> OfValue(T value) {

  }

  public bool TryGetValue(
    [NotNullWhen(true)]out T? value,
    [NotNullWhen(false)]out string? status) {
  }
}
