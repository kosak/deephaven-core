using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
namespace Deephaven.ExcelAddIn.Util;

internal static class RefUtil {
  public static void Replace<T>(ref StatusOr<RefCounted<T>> dest, StatusOr<RefCounted<T>> newValue)
    where T : class, IDisposable {
    if (dest.GetValueOrStatus(out var dv, out _)) {
      Background.InvokeDispose(dv);
    }

    if (newValue.GetValueOrStatus(out var nv, out var status)) {
      dest = nv.Share();
    } else {
      dest = status;
    }
  }

  public static void ReplaceAndNotify<T>(ref StatusOr<RefCounted<T>> dest,
    StatusOr<RefCounted<T>> newValue, ObserverContainer<StatusOr<RefCounted<T>>> observers)
    where T : class, IDisposable {
    Replace(ref dest, newValue);
    observers.OnNext(dest);
    EnqueueKeepAlive(observers, dest);
  }

  public static void AddObserverAndNotify<T>(ObserverContainer<StatusOr<RefCounted<T>>> observers,
    IValueObserver<StatusOr<RefCounted<T>>> observer,
    StatusOr<RefCounted<T>> item,
    out bool isFirst) where T : class, IDisposable {
    observers.AddAndNotify(observer, item, out isFirst);
    EnqueueKeepAlive(observers, item);
  }

  private static void EnqueueKeepAlive<T>(ObserverContainer<StatusOr<RefCounted<T>>> observers,
    StatusOr<RefCounted<T>> item) where T : class, IDisposable {
    if (!item.GetValueOrStatus(out var value, out _)) {
      return;
    }
    // Enqueue a Dispose action for a Share of this item. This will keep the item alive until
    // the ObserverContainer gets to this point.
    var shared = value.Share();
    observers.EnqueueAction(() => Background.InvokeDispose(shared));
  }
}

internal static class StatusOrUtil {
  public static void ReplaceAndNotify<T>(ref StatusOr<T> dest,
    StatusOr<T> newValue, ObserverContainer<StatusOr<T>> observers) {
    AssertNotRefCounted<T>();
    dest = newValue;
    observers.OnNext(dest);
  }

  public static void AddObserverAndNotify<T>(ObserverContainer<StatusOr<T>> observers,
    IValueObserver<StatusOr<T>> observer,
    StatusOr<T> item,
    out bool isFirst) {
    AssertNotRefCounted<T>();
    observers.AddAndNotify(observer, item, out isFirst);
  }

  /// <summary>
  /// Sanity check. Prevents T from being any RefCounted subtype
  /// </summary>
  private static void AssertNotRefCounted<T>() {
    if (typeof(RefCounted).IsAssignableFrom(typeof(T))) {
      throw new Exception("Programming error: Should have invoked the other ReplaceAndNotify method");
    }
  }
}
