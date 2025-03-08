using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Util;

internal class SorUtil {
  public static void Replace<T>(ref StatusOr<T> dest, StatusOr<T> newValue) {
    // Background.InvokeDispose(dest);
    // dest = newValue.Share();
  }

  public static void ReplaceAndNotify<T>(ref StatusOr<T> dest,
    StatusOr<T> newValue, ObserverContainer<StatusOr<T>> container) {
    // Background.InvokeDispose(dest);
    // container.OnNext(newValue);
  }

  public static void AddObserverAndNotify<T>(ObserverContainer<StatusOr<T>> observers,
    IValueObserver<StatusOr<T>> observer,
    StatusOr<T> item,
    out bool isFirst) {
    // Background.InvokeDispose(dest);
    // container.OnStatus(status);
  }
}

