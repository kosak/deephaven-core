namespace Deephaven.ManagedClient;

public class Atomic<T> {
  private readonly object _sync = new();
  private T _value;

  public Atomic(T value) {
    _value = value;
  }

  public T Value {
    get {
      lock (_sync) {
        return _value;
      }
    }
    set {
      lock (_sync) {
        _value = value;
      }
    }
  }
}
