using System.Diagnostics.CodeAnalysis;

namespace Deephaven.ExcelAddIn.Status;

public abstract class StatusOr : IDisposable {
  public abstract StatusOr Share();
  public abstract void Dispose();
}

public sealed class StatusOr<T> : StatusOr {
  private readonly string? _status;
  private readonly T? _value;

  public static implicit operator StatusOr<T>(string s) {

  }

  public static StatusOr<T> OfStatus(string status) {
    return new StatusOr<T>(status, default);
  }

  public static StatusOr<T> OfValue(T value,
    params StatusOr[] dependencies) {
    return new StatusOr<T>(null, value);
  }

  private StatusOr(string? status, T? value) {
    _status = status;
    _value = value;
  }

  public override StatusOr<T> Share() {

  }

  public bool GetValueOrStatus(
    [NotNullWhen(true)]out T? value,
    [NotNullWhen(false)]out string? status) {
    status = _status;
    value = _value;
    return value != null;
  }

  public U AcceptVisitor<U>(Func<T, U> onValue, Func<string, U> onStatus) {
    return _value != null ? onValue(_value) : onStatus(_status!);
  }

  public void Deconstruct(out T value, out string status) {

  }
}
