using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointDictProvider :
  IValueObservable<SharableDict<EndpointConfigEntry>> {
  private readonly object _sync = new();
  private readonly ObserverContainer<SharableDict<EndpointConfigEntry>> _observers = new();
  private SharableDict<EndpointConfigEntry> _dict = new();
  private readonly Dictionary<EndpointId, Int64> _idToKey = new();
  private Int64 _nextFreeId = 0;

  public IDisposable Subscribe(IValueObserver<SharableDict<EndpointConfigEntry>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _dict, out _);
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public bool TryAddEmpty(EndpointId endpointId) {
    lock (_sync) {
      var key = _nextFreeId;
      if (_idToKey.TryAdd(endpointId, key)) {
        // endpointId is already in dictionary
        return false;
      }
      ++_nextFreeId;
      _dict = _dict.With(key, new EndpointConfigEntry(endpointId, null));

      _observers.OnNext(_dict);
      return true;
    }
  }

  public bool InsertOrReplace(EndpointConfigBase config) {
    lock (_sync) {
      var inserted = false;
      if (!_idToKey.TryGetValue(config.Id, out var key)) {
        key = _nextFreeId++;
        _idToKey[config.Id] = key;
        inserted = true;
      }
      var entry = new EndpointConfigEntry(config.Id, config);
      _dict = _dict.With(key, entry);
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
