using System.Diagnostics.CodeAnalysis;

namespace Deephaven.ExcelAddIn.Util;

public sealed record Status(string Text, bool IsFixed) {
  public static Status OfTransient(string text) => new Status(text, false);
  public static Status OfFixed(string text) => new Status(text, true);
}

public sealed class StatusOr<T> {
  private readonly Status? _status;
  private readonly T? _value;

  public static implicit operator StatusOr<T>(Status status) {
    return new StatusOr<T>(status, default);
  }

  public static implicit operator StatusOr<T>(string status) {
    return OfFixed(status);
  }

  public static implicit operator StatusOr<T>(T value) {
    return OfValue(value);
  }

  public static StatusOr<T> OfTransient(string progress) {
    return new StatusOr<T>(Status.OfTransient(progress), default);
  }

  public static StatusOr<T> OfFixed(string state) {
    return new StatusOr<T>(Status.OfFixed(state), default);
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
