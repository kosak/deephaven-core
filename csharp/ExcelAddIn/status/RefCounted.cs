using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn;

internal class RefCountedImpl<T> where T : class, IDisposable {
  private int _refCount = 0;
  private T? _value;

  internal RefCountedImpl(T value) {
    _value = value;
  }

  internal RefCounted<T> Share() {
    Interlocked.Increment(ref _refCount);
    return new RefCounted<T>(this);
  }

  internal void Unshare() {
    var result = Interlocked.Decrement(ref _refCount);
    if (result == 0) {
      Utility.MaybeDispose(ref _value);
    }
  }

  internal T Value => Utility.NotNull(_value);
}

public static class RefCounted {
  public static RefCounted<T> Acquire<T>(T item) where T : class, IDisposable {
    var impl = new RefCountedImpl<T>(item);
    return impl.Share();
  }
}

public class RefCounted<T> : IDisposable where T : class, IDisposable {
  private RefCountedImpl<T>? _impl;

  internal RefCounted(RefCountedImpl<T> impl) {
    _impl = impl;
  }

  public RefCounted<T> Share() {
    return Utility.NotNull(_impl).Share();
  }

  public T Value => Utility.NotNull(_impl).Value;

  public void Dispose() {
    Utility.Exchange(ref _impl, null)?.Unshare();
  }
}

public readonly struct View<T> where T : class, IDisposable {
  private readonly RefCountedImpl<T> _impl;

  internal View(RefCountedImpl<T> impl) {
    _impl = impl;
  }

  public T Value => _impl.Value;

  public RefCounted<T> Share() {
    return _impl.Share();
  }
}
