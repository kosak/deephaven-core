using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using System;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointDictProvider :
  IValueObservable<SharableDict<EndpointConfigBase>>,
  IValueObservable<StatusOr<EndpointId>> {
  private const string UnsetEndpointText = "No endpoint";

  private readonly object _sync = new();
  private readonly ObserverContainer<SharableDict<EndpointConfigBase>> _dictObservers = new();
  private readonly ObserverContainer<StatusOr<EndpointId>> _defaultEpObservers = new();
  private SharableDict<EndpointConfigBase> _dict = new();
  private readonly Dictionary<EndpointId, Int64> _idToKey = new();
  private Int64 _nextFreeId = 0;
  private readonly StatusOrHolder<EndpointId> _endpointId = new(UnsetEndpointText);

  public IObservableCallbacks Subscribe(IValueObserver<SharableDict<EndpointConfigBase>> observer) {
    lock (_sync) {
      _dictObservers.AddAndNotify(observer, _dict, out _);
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<EndpointId>> observer) {
    lock (_sync) {
      _endpointId.AddObserverAndNotify(_defaultEpObservers, observer, out _);
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    // Do nothing.
  }

  private void RemoveObserver(IValueObserver<SharableDict<EndpointConfigBase>> observer) {
    lock (_sync) {
      _dictObservers.Remove(observer, out _);
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<EndpointId>> observer) {
    lock (_sync) {
      _defaultEpObservers.Remove(observer, out _);
    }
  }

  public bool TryAdd(EndpointConfigBase config) {
    return TryAddOrMaybeReplace(config, false, true);
  }
  
  public bool AddOrReplace(EndpointConfigBase config) {
    return TryAddOrMaybeReplace(config, true, true);
  }

  public bool TryAddWithoutNotify(EndpointConfigBase config) {
    return TryAddOrMaybeReplace(config, false, false);
  }

  private bool TryAddOrMaybeReplace(EndpointConfigBase config, bool permitReplace,
    bool wantNotify) {
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
      if (wantNotify) {
        _observers.OnNext(_dict);
      }
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

  public SharableDict<EndpointConfigBase> GetDict() => _dict;

  public EndpointId? DefaultEndpoint {
    get {
      lock (_sync) {
        return _endpointId.GetValueOrStatus(out var value, out _) ? value : null;
      }
    }
  }

  public void SetDefaultEndpoint(EndpointId? endpointId) {
    lock (_sync) {
      if (endpointId == null) {
        _endpointId.ReplaceAndNotify(UnsetEndpointText, _observers);
      } else {
        _endpointId.ReplaceAndNotify(endpointId, _observers);
      }
    }
  }

}
