using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Util;

internal class RefUtil666_NO {
  public static void Replace<T>(ref StatusOr<RefCounted<T>> dest,
    StatusOr<RefCounted<T>> newValue) where T : class, IDisposable {
    Background.InvokeDispose(dest);
    dest = newValue.Share();
  }

  public static void ReplaceAndNotify<T>(ref StatusOr<RefCounted<T>> dest,
    StatusOr<RefCounted<T>> newValue, ObserverContainer<RefCounted<T>> container)
    where T : class, IDisposable {
    Background.InvokeDispose(dest);
    container.OnNext(newValue);
  }

  public static void AddObserverAndNotify<T>(ObserverContainer<RefCounted<T>> observers,
    IStatusObserver<RefCounted<T>> observer,
    StatusOr<RefCounted<T>> item,
    out bool isFirst) where T : class, IDisposable {
    Background.InvokeDispose(dest);
    container.OnStatus(status);
  }
}

internal class SorUtil {
  public static void Replace<T>(ref StatusOr<T> dest, StatusOr<T> newValue) {
    Background.InvokeDispose(dest);
    dest = newValue.Share();
  }

  public static void ReplaceAndNotify<T>(ref StatusOr<T> dest,
    StatusOr<T> newValue, ObserverContainer<StatusOr<T>> container) {
    Background.InvokeDispose(dest);
    container.OnNext(newValue);
  }

  public static void AddObserverAndNotify<T>(ObserverContainer<T> observers,
    IStatusObserver<T> observer,
    StatusOr<T> item,
    out bool isFirst) {
    Background.InvokeDispose(dest);
    container.OnStatus(status);
  }
}

