using System.Diagnostics.CodeAnalysis;

namespace Deephaven.ExcelAddIn.Status;

public sealed record Status(string Text, bool IsState) {
  public static Status OfProgress(string text) => new Status(text, false);
  public static Status OfState(string text) => new Status(text, true);
}

public sealed class StatusOr<T> {
  private readonly Status? _status;
  private readonly T? _value;

  public static implicit operator StatusOr<T>(string status) {
    return OfStatus(status);
  }

  public static implicit operator StatusOr<T>(T value) {
    return OfValue(value);
  }

  public static StatusOr<T> OfProgress(string progress) {
    return new StatusOr<T>(Status.OfProgress(progress), default);
  }

  public static StatusOr<T> OfState(string state) {
    return new StatusOr<T>(Status.OfState(state), default);
  }

  public static StatusOr<T> OfValue(T value) {
    return new StatusOr<T>(null, value);
  }

  private StatusOr(Status? status, T? value) {
    _status = status;
    _value = value;
  }

  public bool GetValueOrStatus(
    [NotNullWhen(true)]out T? value,
    [NotNullWhen(false)]out Status? status) {
    status = _status;
    value = _value;
    return status == null;
  }

  public U AcceptVisitor<U>(Func<T, U> onValue, Func<Status, U> onStatus) {
    return _status == null ? onValue(_value!) : onStatus(_status);
  }

  public void Deconstruct(out T? value, out Status? status) {
    value = _value;
    status = _status;
  }
}
