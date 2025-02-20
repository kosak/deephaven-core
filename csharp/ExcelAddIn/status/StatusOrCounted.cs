using System.Diagnostics.CodeAnalysis;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn;

internal class RefCountedImpl<T> where T : class, IDisposable {
  private int _refCount = 0;
  private T? _value;

  internal RefCountedImpl(T value) {
    _value = value;
  }

  internal StatusOrCounted<T> Share() {
    Interlocked.Increment(ref _refCount);
    return new StatusOrCounted<T>(this);
  }

  internal void Unshare() {
    var result = Interlocked.Decrement(ref _refCount);
    if (result == 0) {
      Utility.MaybeDispose(ref _value);
    }
  }

  internal T Value => Utility.NotNull(_value);
}

public static class StatusOrCounted {
  // public static StatusOrCounted<T> Acquire<T>(T item) where T : class, IDisposable {
  //   var impl = new RefCountedImpl<T>(item);
  //   return impl.Share();
  // }

  public static void ResetWithStatus<T>(ref T result, string status) {
    var newValue = StatusOrCounted<T>.OfStatus(status);
    Utility.Swap(ref result, ref newValue);
    Utility.DisposeInBackground(newValue);
  }
}

public class StatusOrCounted<T> : IDisposable where T : class, IDisposable {
  private readonly string? _status;
  private readonly RefCountedImpl<T>? _impl;

  public static StatusOrCounted<T> OfStatus(string status) {
    return new StatusOrCounted<T>(status, null);
  }

  public static StatusOrCounted<T> Acquire(T value, params IDisposable[] dependencies) {
    var impl = new RefCountedImpl<T>(value, dependencies);
    return new StatusOrCounted<T>(null, impl);
  }

  private StatusOrCounted(string? status, RefCountedImpl<T>? impl) {
    _status = status;
    _impl = impl;
  }

  public bool GetValueOrStatus(
    [NotNullWhen(true)] out T? value,
    [NotNullWhen(false)] out string? status) {
    status = _status;
    value = _impl?.Value;
    return value != null;
  }

  // internal StatusOrCounted(RefCountedImpl<T> impl) {
  //   _impl = impl;
  // }
  //
  public StatusOrCounted<T> Share() {
    if (_status != null) {
      // If we are holding status, we don't need to clone.
      return this;
    }

    _impl.Increment();
    return new StatusOrCounted(null, _impl);
  }

  //
  // public T Value => Utility.NotNull(_impl).Value;
  //
  // public void Dispose() {
  //   Utility.Exchange(ref _impl, null)?.Unshare();
  // }
}
