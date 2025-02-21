namespace Deephaven.ExcelAddIn.Status;

public static class StatusOrCounted {
  public static StatusOr<RefCounted<T>> Empty<T>() where T : class, IDisposable {
    return StatusOr<RefCounted<T>>.OfStatus("");
  }

  public static void ReplaceWithStatus<T>(ref StatusOr<RefCounted<T>> target, string statusMessage)
    where T : class, IDisposable {
    Background.Dispose(target.AsDisposable());
    target = StatusOr<RefCounted<T>>.OfStatus(statusMessage);
  }

  public static void ReplaceWithValue<T>(ref StatusOr<RefCounted<T>> target, T value,
    params IDisposable[] dependencies)
    where T : class, IDisposable {
    Background.Dispose(target.AsDisposable());
    var rct = RefCounted<T>.Acquire(value, dependencies);
    target = StatusOr<RefCounted<T>>.OfValue(rct);
  }

  public static void ReplaceWith<T>(ref StatusOr<RefCounted<T>> target,
    StatusOr<View<T>> view) where T : class, IDisposable {
    Background.Dispose(target.AsDisposable());
    target = Share(view);
  }

  /// <summary>
  /// Shares a StatusOr&lt;RefCounted&lt;T&gt;&gt;
  /// </summary>
  public static StatusOr<RefCounted<T>> Share<T>(this StatusOr<RefCounted<T>> item)
    where T : class, IDisposable {
    // Status items can be shared identically (by reference), without cloning.
    return item.GetValueOrStatus(out var rct, out _)
      ? StatusOr<RefCounted<T>>.OfValue(rct.Share())
      : item;
  }

  /// <summary>
  /// Shares a StatusOr&lt;View&lt;T&gt;&gt;
  /// </summary>
  public static StatusOr<RefCounted<T>> Share<T>(this StatusOr<View<T>> item)
    where T : class, IDisposable {
    return item.GetValueOrStatus(out var rct, out var status)
      ? StatusOr<RefCounted<T>>.OfValue(rct.Share())
      : StatusOr<RefCounted<T>>.OfStatus(status);
  }


  /// <summary>
  /// Turns a StatusOr&lt;RefCounted&lt;T&gt;&gt; into a StatusOr&lt;View&lt;T&gt;&gt;
  /// </summary>
  public static StatusOr<View<T>> AsView<T>(this StatusOr<RefCounted<T>> item)
    where T : class, IDisposable {
    return item.GetValueOrStatus(out var rct, out var status)
      ? StatusOr<View<T>>.OfValue(rct.View())
      : StatusOr<View<T>>.OfStatus(status);
  }

  /// <summary>
  /// Turns a StatusOr&lt;RefCounted&lt;T&gt;&gt; into an IDisposable
  /// </summary>
  public static IDisposable AsDisposable<T>(this StatusOr<RefCounted<T>> item)
    where T : class, IDisposable {
    return item.GetValueOrStatus(out var rct, out _) ? rct : NullDisposable.Instance;
  }
}
