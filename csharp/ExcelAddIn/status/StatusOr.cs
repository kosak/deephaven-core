using System.Diagnostics.CodeAnalysis;

namespace Deephaven.ExcelAddIn.Status;

public sealed class StatusOr<T> {
  private readonly string? _status;
  private readonly T? _value;

  public static implicit operator StatusOr<T>(string status) {
    return OfStatus(status);
  }

  public static implicit operator StatusOr<T>(T value) {
    return OfValue(value);
  }

  public static StatusOr<T> OfStatus(string status) {
    return new StatusOr<T>(status, default);
  }

  public static StatusOr<T> OfValue(T value) {
    return new StatusOr<T>(null, value);
  }

  private StatusOr(string? status, T? value) {
    _status = status;
    _value = value;
  }

  public bool GetValueOrStatus(
    [NotNullWhen(true)]out T? value,
    [NotNullWhen(false)]out string? status) {
    status = _status;
    value = _value;
    return status == null;
  }

  public U AcceptVisitor<U>(Func<T, U> onValue, Func<string, U> onStatus) {
    return _status == null ? onValue(_value!) : onStatus(_status);
  }

  public void Deconstruct(out T? value, out string? status) {
    value = _value;
    status = _status;
  }
}
