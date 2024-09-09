namespace Deephaven.ExcelAddIn.Util;

internal class SharedCountImpl {
  private long _refCount = 1;
  private readonly RefCounted[] _dependencies;

  public SharedCountImpl(RefCounted[] dependencies) {
    _dependencies = dependencies;
  }

  public void Increment() {
    var result = Interlocked.Increment(ref _refCount);
    if (result < 2) {
      // Incremented from 0 to 1, or from some negative number.
      throw new Exception($"Bad state: {result}");
    }
  }

  public void DecrementAndMaybeDispose(IDisposable value) {
    var result = Interlocked.Decrement(ref _refCount);
    if (result > 0) {
      return;
    }
    if (result < 0) {
      // Decremented a zero or negative number.
      throw new Exception($"Bad state: decremented too many times: {result}");
    }

    // Dispose the value first, then its dependencies
    value.Dispose();
    for (var i = 0; i != _dependencies.Length; i++) {
      _dependencies[i].Dispose();
    }
  }
}

public abstract class RefCounted : IDisposable {
  private protected SharedCountImpl? _sharedCount;

  private protected RefCounted(SharedCountImpl sharedCount) {
    _sharedCount = sharedCount;
  }

  internal static RefCounted<T> Acquire<T>(T value, params RefCounted[] dependencies)
    where T : class, IDisposable {
    var impl = new SharedCountImpl(dependencies);
    return new RefCounted<T>(value, impl);
  }

  public abstract void Dispose();
  public abstract RefCounted Share();

  private protected SharedCountImpl SharedCount {
    get {
      CheckDisposed();
      return _sharedCount!;
    }
  }

  protected void CheckDisposed() {
    if (_sharedCount == null) {
      throw new Exception("Accessing disposed object");
    }
  }

}

public sealed class RefCounted<T> : RefCounted where T : class, IDisposable {
  internal static RefCounted<T> CastAndShare<TChild>(RefCounted<TChild> child)
        where TChild : class, IDisposable, T {
    var sc = child.SharedCount;
    sc.Increment();
    return new RefCounted<T>(child.Value, sc);
  }

  private readonly T _value;

  internal RefCounted(T value, SharedCountImpl impl) : base(impl) {
    _value = value;
  }

  public override void Dispose() {
    var temp = Utility.Exchange(ref _sharedCount, null);
    temp?.DecrementAndMaybeDispose(Value);
  }

  public T Value {
    get {
      CheckDisposed();
      return _value;
    }
  }

  public override RefCounted<T> Share() {
    SharedCount.Increment();
    return new RefCounted<T>(Value, SharedCount);
  }
}
