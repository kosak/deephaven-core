using System.Diagnostics.CodeAnalysis;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Providers;

namespace Deephaven.ExcelAddIn.Util;

public class StatusOrHolder<T> {
  private StatusOr<T> _value;
  private IDisposable? _sharedDisposer = null;

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
  /// First unshares the old value by calling its disposer (if it exists). Then if
  /// newValue contains a value and that value is IDisposable, then assume it is
  /// participating in our Sharing protocol, so call Repository.Share() on it.
  /// </summary>
  public void Replace(StatusOr<T> newValue) {
    Utility.ClearAndDispose(ref _sharedDisposer);

    if (newValue.GetValueOrStatus(out var nv, out _) && nv is IDisposable disp) {
      _sharedDisposer = Repository.Share(disp);
    }
    _value = newValue;
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
    // We are, so we need to reshare it
    if (!_value.GetValueOrStatus(out var value, out _) || value is not IDisposable disp) {
      return;
    }

    // We are holding a sharable value, so we share it and post the disposer to
    // the ObserverContainer. This keeps the previously-posted values alive until
    // at least the point of the posting. We say "at least" because the Action we
    // post is a Background action, so it will actually run at some later point.
    // We do this because we don't want a long-running disposer to hold up the
    // ObserverContainer.
    var sharedDisposer = Repository.Share(disp);
    observers.EnqueueAction(() => Background.InvokeDispose(sharedDisposer));
  }
}
