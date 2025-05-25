using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class RetryProvider :
  IValueObservable<RetryPlaceholder> {
  private readonly object _sync = new();
  private readonly ObserverContainer<RetryPlaceholder> _observers = new();

  public IDisposable Subscribe(IValueObserver<RetryPlaceholder> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, new RetryPlaceholder(), out _);
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public void Notify() {
    lock (_sync) {
      _observers.OnNext(new RetryPlaceholder());
    }
  }
}
