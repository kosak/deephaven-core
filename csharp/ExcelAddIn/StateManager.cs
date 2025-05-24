using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn;

public class StateManager {
  private readonly object _sync = new();
  private readonly ReferenceCountingDict<EndpointId, CoreClientProvider> _coreClientProviders = new();
  private readonly ReferenceCountingDict<(EndpointId, PqName), CorePlusClientProvider> _corePlusClientProviders = new();
  private readonly ReferenceCountingDict<EndpointId, EndpointConfigProvider> _endpointConfigProviders = new();
  private readonly ReferenceCountingDict<EndpointId, EndpointHealthProvider> _endpointHealthProviders = new();
  private readonly ReferenceCountingDict<TableQuad, ITableProviderBase> _tableProviders = new();
  private readonly ReferenceCountingDict<EndpointId, PqDictProvider> _persistentQueryDictProviders = new();
  private readonly ReferenceCountingDict<(EndpointId, PqName), PqInfoProvider> _persistentQueryInfoProviders = new();
  private readonly ReferenceCountingDict<EndpointId, SessionManagerProvider> _sessionManagerProviders = new();
  private readonly ReferenceCountingDict<EndpointId, PqSubscriptionProvider> _subscriptionProviders = new();

  private readonly DefaultEndpointProvider _defaultEndpointProvider = new();
  private readonly EndpointDictProvider _endpointDictProvider = new();

  public IDisposable SubscribeToCoreClient(EndpointId endpointId,
    IValueObserver<StatusOr<RefCounted<Client>>> observer) {
    var candidate = new CoreClientProvider(this, endpointId);
    return SubscribeHelper(_coreClientProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToCorePlusClient(EndpointId endpointId, PqName pqName,
    IValueObserver<StatusOr<RefCounted<DndClient>>> observer) {
    var key = (endpointId, pqName);
    var candidate = new CorePlusClientProvider(this, endpointId, pqName);
    return SubscribeHelper(_corePlusClientProviders, key, candidate, observer);
  }

  public IDisposable SubscribeToEndpointConfig(EndpointId endpointId,
    IValueObserver<StatusOr<EndpointConfigBase>> observer) {
    var candidate = new EndpointConfigProvider(this, endpointId);
    return SubscribeHelper(_endpointConfigProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToEndpointHealth(EndpointId endpointId,
    IValueObserver<StatusOr<EndpointHealth>> observer) {
    var candidate = new EndpointHealthProvider(this, endpointId);
    return SubscribeHelper(_endpointHealthProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToTable(TableQuad key,
    IValueObserver<StatusOr<RefCounted<TableHandle>>> observer) {
    ITableProviderBase candidate;
    if (key.EndpointId == null) {
      candidate = new DefaultEndpointTableProvider(this, key.PqName, key.TableName, key.Condition);
    } else if (key.Condition.Length != 0) {
      candidate = new FilteredTableProvider(this, key.EndpointId, key.PqName, key.TableName,
        key.Condition);
    } else {
      candidate = new TableProvider(this, key.EndpointId, key.PqName, key.TableName);
    };
    return SubscribeHelper(_tableProviders, key, candidate, observer);
  }

  public IDisposable SubscribeToPersistentQueryDict(EndpointId endpointId,
    IValueObserver<StatusOr<SharableDict<PersistentQueryInfoMessage>>> observer) {
    var candidate = new PqDictProvider(this, endpointId);
    return SubscribeHelper(_persistentQueryDictProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToPersistentQueryInfo(EndpointId endpointId, PqName pqName,
    IValueObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
    var key = (endpointId, pqName);
    var candidate = new PqInfoProvider(this, endpointId, pqName);
    return SubscribeHelper(_persistentQueryInfoProviders, key, candidate, observer);
  }

  public IDisposable SubscribeToSessionManager(EndpointId endpointId,
    IValueObserver<StatusOr<RefCounted<SessionManager>>> observer) {
    var candidate = new SessionManagerProvider(this, endpointId);
    return SubscribeHelper(_sessionManagerProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToSubscription(EndpointId endpointId,
    IValueObserver<StatusOr<RefCounted<Subscription>>> observer) {
    var candidate = new PqSubscriptionProvider(this, endpointId);
    return SubscribeHelper(_subscriptionProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToDefaultEndpoint(IValueObserver<StatusOr<EndpointId>> observer) {
    return _defaultEndpointProvider.Subscribe(observer);
  }

  public void SetDefaultEndpoint(EndpointId? defaultEndpointId) {
    _defaultEndpointProvider.Set(defaultEndpointId);
  }

  public IDisposable SubscribeToEndpointDict(
    IValueObserver<SharableDict<EndpointConfigBase>> observer) {
    return _endpointDictProvider.Subscribe(observer);
  }

  public void SetConfig(EndpointConfigBase config) {
    lock (_sync) {
      _endpointDictProvider.AddOrReplace(config);
    }
  }

  public void EnsureConfig(EndpointId id) {
    lock (_sync) {
      var empty = EndpointConfigBase.OfEmpty(id);
      _ = _endpointDictProvider.TryAdd(empty);
    }
  }

  public bool TryDeleteConfig(EndpointId id) {
    lock (_sync) {
      if (_endpointConfigProviders.ContainsKey(id)) {
        // Someone is still referencing it, so it's unsafe to delete
        return false;
      }
      return _endpointDictProvider.TryRemove(id);
    }
  }

  public void Reconnect(EndpointId id) {
    // Quick-and-dirty trick for reconnect is to re-send the credentials to the observers.
    lock (_sync) {
      if (_endpointConfigProviders.TryGetValue(id, out var cp)) {
        cp.Resend();
      }
    }
  }

  private IDisposable SubscribeHelper<TKey, TObservable, T>(
    ReferenceCountingDict<TKey, TObservable> dict,
    TKey key, TObservable candidateObservable, IValueObserver<T> observer)
    where TKey : notnull
    where TObservable : IValueObservable<T> {
    TObservable actualObservable;
    lock (_sync) {
      _ = dict.AddOrIncrement(key, candidateObservable, out actualObservable);
    }

    // Subscribe the observer to the (new or existing) observable
    var disposer = actualObservable.Subscribe(observer);

    // Now make a dispose action, which needs to
    // 1. If called more than once, do nothing on subsequent calls
    // 2. If the reference count hits zero, remove from the dictionary
    // 3. Call disposer.Dispose()

    var isDisposed = false;
    return ActionAsDisposable.Create(() => {
      // Decrement or remove entry from dictionary
      lock (_sync) {
        if (Utility.Exchange(ref isDisposed, true)) {
          return;
        }
        _ = dict.DecrementOrRemove(key);
      }

      // Unsubscribe the observer from the observable
      disposer.Dispose();
    });
  }
}
