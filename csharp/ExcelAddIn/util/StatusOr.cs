using System.Diagnostics.CodeAnalysis;

namespace Deephaven.ExcelAddIn.Util;

public class StatusOr<T> {
  private readonly string? _status;
  private readonly T? _value;

  public static StatusOr<T> OfStatus(string status) {
    return new StatusOr<T>(status, default);
  }

  public static StatusOr<T> OfValue(T value) {
    return new StatusOr<T>(null, value);
  }

  public StatusOr(string? status, T? value) {
    _status = status;
    _value = value;
  }

  public bool GetValueOrStatus(
    [NotNullWhen(true)]out T? value,
    [NotNullWhen(false)]out string? status) {
    status = _status;
    value = _value;
    return value != null;
  }
}
