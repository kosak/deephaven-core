using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Util;

internal class SorUtil {
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


  public static void SetStatusAndNotify<T>(ref StatusOr<RefCounted<T>> dest,
    string status, ObserverContainer<RefCounted<T>> container) where T : class, IDisposable {
    Background.InvokeDispose(dest);
    container.OnStatus(status);
  }

  public static void SetStateAndNotify<T>(ref StatusOr<RefCounted<T>> dest,
    StatusOr<RefCounted<T>> newValue,
    ObserverContainer<T> container) where T : class, IDisposable {
    Background.InvokeDispose(dest);
    container.OnNext(newValue);
  }

}
