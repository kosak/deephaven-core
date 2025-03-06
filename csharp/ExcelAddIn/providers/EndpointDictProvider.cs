using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointDictProvider :
  IObservable<SharableDict<EndpointConfigBase>> {
  private readonly object _sync = new();
  private readonly ObserverContainer<SharableDict<EndpointConfigBase>> _observers = new();
  private SharableDict<EndpointConfigBase> _dict = new();
  private readonly Dictionary<EndpointId, Int64> _idToKey = new();
  private Int64 _nextFreeId = 0;

  public IDisposable Subscribe(IObserver<SharableDict<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _dict, out _);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<SharableDict<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public bool TryAddEmpty(EndpointId endpointId) {
    lock (_sync) {
      var key = _nextFreeId;
      if (_idToKey.TryAdd(endpointId, key)) {
        // endpointId is already in dictionary
        return false;
      }
      ++_nextFreeId;
      var newValue = EndpointConfigBase.OfEmpty(endpointId);
      _dict = _dict.With(key, newValue);

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
