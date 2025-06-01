using System.Diagnostics.CodeAnalysis;
using Deephaven.ExcelAddIn.Observable;

namespace Deephaven.ExcelAddIn.Util;

public class StatusOrHolder<T> {
  private StatusOr<T> _value;

  public StatusOrHolder(string state) {
    _value = StatusOr<T>.OfFixed(state);
  }

  /// <summary>
  /// Convenience
  /// </summary>
  public bool GetValueOrStatus(
    [NotNullWhen(true)] out T? value,
    [NotNullWhen(false)] out Status? status) {
    return _value.GetValueOrStatus(out value, out status);
  }

  /// <summary>
  /// First discards the old value of dest (which will dispose it if it contains
  /// a value, and that value is a RefCounted type).
  /// Then if newValue contains a value and that value is RefCounted, calls Share()
  /// to share that RefCounted item, and stores the resultant StatusOr. Otherwise,
  /// just stores newValue. The rationale here is that StatusOr acts like an
  /// immutable type unless it contains an IDisposable or a RefCounted, in which
  /// case it participates in the RefCounted protocol.
  /// </summary>
  public void Replace(StatusOr<T> newValue) {
    if (_value.GetValueOrStatus(out var dv, out _) && dv is RefCounted destRc) {
      Background.InvokeDispose(destRc);
    }

    if (newValue.GetValueOrStatus(out var sv, out _) && sv is RefCounted srcSc) {
      _value = (T)(object)srcSc.Share();
    } else {
      // For other cases (status-containing, or value containing but not RefCounted)
      // you can treat the StatusOr as an freely-copyable value type.
      _value = newValue;
    }
  }

  public void Notify(ObserverContainer<StatusOr<T>> observers) {
    observers.OnNext(_value);
    EnqueueKeepAlive(observers);
  }

  public void ReplaceAndNotify(StatusOr<T> newValue,
    ObserverContainer<StatusOr<T>> observers) {
    Replace(newValue);
    observers.OnNext(newValue);
    EnqueueKeepAlive(observers);
  }

  public void AddObserverAndNotify(ObserverContainer<StatusOr<T>> observers,
    IValueObserver<StatusOr<T>> observer, out bool isFirst) {
    observers.AddAndNotify(observer, _value, out isFirst);
    EnqueueKeepAlive(observers);
  }

  private void EnqueueKeepAlive(ObserverContainer<StatusOr<T>> observers) {
    if (!_value.GetValueOrStatus(out var value, out _) || value is not RefCounted rc) {
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
