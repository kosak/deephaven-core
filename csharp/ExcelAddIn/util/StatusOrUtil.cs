namespace Deephaven.ExcelAddIn.Util;

/// <summary>
/// This is a set of utility functions for working with StatusOr&lt;T&gt;.
/// The reason this exists at all is because, for consistency, we would like
/// to use the same set of helper functions for StatusOr&lt;T&gt; and
/// StatusOr&lt;RefCounted&lt;T&gt;&gt;
/// In the latter case we need to do special work to dispose, share, and manage
/// the lifetime of the RefCounted item.
/// Implementation note: you *can* do this with generics, e.g. by having
/// two Replace overloads, one taking StatusOr&lt;T&gt; and one taking
/// StatusOr&lt;RefCounted&lt;T&gt;&gt;, but I'm a little worried about
/// the danger of managing to accidentally call the former when you meant the latter.
/// </summary>
internal static class StatusOrUtil {
  /// <summary>
  /// First discards the old value of dest (which will dispose it if it contains
  /// a value, and that value is a RefCounted type).
  /// Then if newValue contains a value and that value is RefCounted, calls Share()
  /// to share that RefCounted item, and stores the resultant StatusOr. Otherwise,
  /// just stores newValue. The rationale here is that StatusOr acts like an
  /// immutable type unless it contains an IDisposable or a RefCounted, in which
  /// case it participates in the RefCounted protocol.
  /// </summary>
  public static void Replace<T>(ref StatusOr<T> dest, StatusOr<T> src) {
    if (dest.GetValueOrStatus(out var dv, out _) && dv is RefCounted destRc) {
      Background.InvokeDispose(destRc);
    }

    if (src.GetValueOrStatus(out var sv, out _) && sv is RefCounted srcSc) {
      dest = (T)(object)srcSc.Share();
    } else {
      // For other cases (status-containing, or value containing but not RefCounted)
      // you can treat the StatusOr as an freely-copyable value type.
      dest = src;
    }
  }

  public static void ReplaceAndNotify<T>(ref StatusOr<T> dest,
    StatusOr<T> newValue, ObserverContainer<StatusOr<T>> observers) {
    Replace(ref dest, newValue);
    observers.OnNext(dest);
    EnqueueKeepAlive(observers, dest);
  }

  public static void AddObserverAndNotify<T>(ObserverContainer<StatusOr<T>> observers,
    IValueObserver<StatusOr<T>> observer, StatusOr<T> item, out bool isFirst) {
    observers.AddAndNotify(observer, item, out isFirst);
    EnqueueKeepAlive(observers, item);
  }

  private static void EnqueueKeepAlive<T>(ObserverContainer<StatusOr<T>> observers,
    StatusOr<T> item) {
    if (!item.GetValueOrStatus(out var value, out _) || value is not RefCounted rc) {
      return;
    }
    // If item contains a value, and if that value is a RefCounted type, then
    // Share() the value and post its disposer to the ObserverContainer. Since
    // the ObserverContainer processes items in order, this will keep the
    // previously-posted values alive until at least this point.
    var shared = rc.Share();
    observers.EnqueueAction(() => Background.InvokeDispose(shared));
  }
}
