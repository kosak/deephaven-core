using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class OpStatusDictProvider :
  IValueObservable<SharableDict<OpStatus>> {
  private readonly object _sync = new();
  private readonly ObserverContainer<SharableDict<OpStatus>> _observers = new();
  private SharableDict<OpStatus> _dict = new();

  public IDisposable Subscribe(IValueObserver<SharableDict<OpStatus>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _dict, out _);
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public void Add(Int64 key, OpStatus value) {
    lock (_sync) {
      _dict = _dict.With(key, value);
      _observers.OnNext(_dict);
    }
  }

  public void Remove(Int64 key) {
    lock (_sync) {
      _dict = _dict.Without(key);
      _observers.OnNext(_dict);
    }
  }
}
