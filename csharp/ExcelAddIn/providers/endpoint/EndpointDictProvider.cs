using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointDictProvider :
  IValueObservable<SharableDict<EndpointConfigBase>> {
  private readonly object _sync = new();
  private readonly ObserverContainer<SharableDict<EndpointConfigBase>> _observers = new();
  private SharableDict<EndpointConfigBase> _dict = new();
  private readonly Dictionary<EndpointId, Int64> _idToKey = new();
  private Int64 _nextFreeId = 0;

  public IDisposable Subscribe(IValueObserver<SharableDict<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _dict, out _);
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public bool TryAdd(EndpointConfigBase config) {
    return TryAddOrMaybeReplace(config, false);
  }

  public bool AddOrReplace(EndpointConfigBase config) {
    return TryAddOrMaybeReplace(config, true);
  }

  private bool TryAddOrMaybeReplace(EndpointConfigBase config, bool permitReplace) {
    lock (_sync) {
      var inserted = false;
      if (!_idToKey.TryGetValue(config.Id, out var key)) {
        // Key does not exist, so allocate one.
        key = _nextFreeId++;
        _idToKey[config.Id] = key;
        inserted = true;
      }
      if (!inserted && !permitReplace) {
        return false;
      }
      _dict = _dict.With(key, config);
      _observers.OnNext(_dict);
      return inserted;
    }
  }


  public bool TryRemove(EndpointId endpointId) {
    lock (_sync) {
      if (!_idToKey.Remove(endpointId, out var removedKey)) {
        return false;
      }
      _dict = _dict.Without(removedKey);
      _observers.OnNext(_dict);
      return true;
    }
  }
}
