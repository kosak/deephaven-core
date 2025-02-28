using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointDictProvider :
  IObservable<IReadOnlyDictionary<Int64, EndpointConfigBase?>> {
  private readonly object _sync = new();
  private readonly ObserverContainer<IReadOnlyDictionary<Int64, EndpointConfigBase?>> _observers = new();
  private SharableDict<EndpointConfigBase?> _dict = new();
  private readonly Dictionary<EndpointId, Int64> _idToKey = new();
  private Int64 _nextFreeId = 0;

  public IDisposable Subscribe(IObserver<IReadOnlyDictionary<Int64, EndpointConfigBase?>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _dict, out var isFirst);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<IReadOnlyDictionary<Int64, EndpointConfigBase?>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
    }
  }

  public bool TryAddEmpty(EndpointId endpointId) {
    lock (_sync) {
      var key = _nextFreeId;
      if (_idToKey.TryAdd(endpointId, key)) {
        // Key already exists.
        return false;
      }
      ++_nextFreeId;
      _dict = _dict.With(key, null);

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

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
