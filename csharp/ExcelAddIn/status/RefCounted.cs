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
