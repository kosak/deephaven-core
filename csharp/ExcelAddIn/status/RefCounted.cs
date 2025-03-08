namespace Deephaven.ExcelAddIn;

internal class RefCountedImpl<T> where T : class, IDisposable {
  private long _refCount = 1;
  private readonly T _value;
  private readonly RefCounted[] _dependencies;

  internal RefCountedImpl(T value, RefCounted[] dependencies) {
    _value = value;
    _dependencies = dependencies;
  }

  internal void Increment() {
    var result = Interlocked.Increment(ref _refCount);
    if (result < 2) {
      // Incremented from 0 to 1, or from some negative number.
      throw new Exception($"Bad state: {result}");
    }
  }

  internal void Decrement() {
    var result = Interlocked.Decrement(ref _refCount);
    if (result > 0) {
      return;
    }
    if (result < 0) {
      // Decremented a zero or negative number.
      throw new Exception($"Bad state: {result}");
    }

    // Dispose the value first, then its dependencies
    _value.Dispose();
    for (var i = 0; i != _dependencies.Length; i++) {
      _dependencies[i].Dispose();
    }
  }

  internal T Value {
    get {
      if (Interlocked.Read(ref _refCount) <= 0) {
        throw new Exception($"Bad state: {_refCount}");
      }
      return _value!;
    }
  }
}

public abstract class RefCounted : IDisposable {
  protected bool IsDisposed = false;

  public static RefCounted<T> Acquire<T>(T value, params RefCounted[] dependencies)
    where T : class, IDisposable {
    return RefCounted<T>.Acquire(value, dependencies);
  }

  public abstract void Dispose();
  public abstract RefCounted Share();
}

public sealed class RefCounted<T> : RefCounted where T : class, IDisposable {
  public static RefCounted<T> Acquire(T value, params RefCounted[] dependencies) {
    var impl = new RefCountedImpl<T>(value, dependencies);
    return new RefCounted<T>(impl);
  }

  private readonly RefCountedImpl<T> _impl;

  private RefCounted(RefCountedImpl<T> impl) {
    _impl = impl;
  }

  public override void Dispose() {
    if (Utility.Exchange(ref IsDisposed, true)) {
      return;
    }
    _impl.Decrement();
  }

  public T Value => _impl.Value;

  public override RefCounted<T> Share() {
    _impl.Increment();
    return new RefCounted<T>(_impl);
  }
}
