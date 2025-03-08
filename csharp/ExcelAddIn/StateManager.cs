using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
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
  private readonly ReferenceCountingDict<EndpointId, PersistentQueryDictProvider> _persistentQueryDictProviders = new();
  private readonly ReferenceCountingDict<(EndpointId, PqName), PersistentQueryInfoProvider> _persistentQueryInfoProviders = new();
  private readonly ReferenceCountingDict<EndpointId, SessionManagerProvider> _sessionManagerProviders = new();
  private readonly ReferenceCountingDict<EndpointId, SubscriptionProvider> _subscriptionProviders = new();

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
    // As a value-added behavior, any request for an EndpointId gets a placeholder
    // in the endpoint dictionary (if it's not already there). The symmetric behavior
    // is deliberately NOT supported: the item is not removed from the endpoint dictionary
    // unless TryDeleteEndpointConfig is called (and that in turn only succeeds if
    // there are no subscribers to the endpoint config).
    _ = _endpointDictProvider.TryAddEmpty(endpointId);

    var candidate = new EndpointConfigProvider(this, endpointId);
    return SubscribeHelper(_endpointConfigProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToEndpointHealth(EndpointId endpointId,
    IValueObserver<EndpointHealth> observer) {
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
    IValueObserver<SharableDict<PersistentQueryInfoMessage>> observer) {
    var candidate = new PersistentQueryDictProvider(this, endpointId);
    return SubscribeHelper(_persistentQueryDictProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToPersistentQueryInfo(EndpointId endpointId, PqName pqName,
    IValueObserver<PersistentQueryInfoMessage> observer) {
    var key = (endpointId, pqName);
    var candidate = new PersistentQueryInfoProvider(this, endpointId, pqName);
    return SubscribeHelper(_persistentQueryInfoProviders, key, candidate, observer);
  }

  public IDisposable SubscribeToSessionManager(EndpointId endpointId,
    IValueObserver<StatusOr<RefCounted<SessionManager>>> observer) {
    var candidate = new SessionManagerProvider(this, endpointId);
    return SubscribeHelper(_sessionManagerProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToSubscription(EndpointId endpointId,
    IValueObserver<StatusOr<Subscription>> observer) {
    var candidate = new SubscriptionProvider(this, endpointId);
    return SubscribeHelper(_subscriptionProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToDefaultEndpoint(IValueObserver<StatusOr<EndpointId>> observer) {
    return _defaultEndpointProvider.Subscribe(observer);
  }

  public void SetDefaultEndpoint(EndpointId? defaultEndpointId) {
    _defaultEndpointProvider.Set(defaultEndpointId);
  }

  public IDisposable SubscribeToEndpointDict(
    IValueObserver<SharableDict<EndpointConfigEntry>> observer) {
    return _endpointDictProvider.Subscribe(observer);
  }

  public bool TryDeleteEndpointConfig(EndpointId id) {
    lock (_sync) {
      if (_endpointConfigProviders.ContainsKey(id)) {
        // Someone is still referencing it, so it's unsafe to delete
        return false;
      }
      return _endpointDictProvider.TryRemove(id);
    }
  }

#if false
  public void SetCredentials(EndpointConfigBase config) {
    LookupOrCreateEndpointConfigProvider(config.Id,
      cp => cp.SetCredentials(config));
  }

  public void Reconnect(EndpointId id) {
    // Quick-and-dirty trick for reconnect is to re-send the credentials to the observers.
    LookupOrCreateEndpointConfigProvider(id, cp => cp.Resend());
  }


  private void LookupOrCreateEndpointConfigProvider(EndpointId endpointId,
    Action<EndpointConfigProvider> action) {
    if (WorkerThread.EnqueueOrNop(() => LookupOrCreateEndpointConfigProvider(endpointId, action))) {
      return;
    }
    if (!_endpointConfigProviders.TryGetValue(endpointId, out var cp)) {
      cp = new EndpointConfigProvider(this);
      _endpointConfigProviders.Add(endpointId, cp);
      cp.Init();
      _endpointConfigPopulationObservers.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
    }

    action(cp);
  }
#endif

  private IDisposable SubscribeHelper<TKey, TObservable, T>(
    ReferenceCountingDict<TKey, TObservable> dict,
    TKey key, TObservable candidateObservable, IValueObserver<T> observer)
    where TKey : notnull
    where TObservable : IValueObservable<T>, IDisposable {
    TObservable actualObservable;
    bool candidateAdded;
    lock (_sync) {
      candidateAdded = dict.AddOrIncrement(key, candidateObservable, out actualObservable);
    }

    if (!candidateAdded) {
      // If we didn't use 'candidateObservable', dispose it
      candidateObservable.Dispose();
    }

    // Subscribe the observer to the (new or existing) observable
    var disposer = actualObservable.Subscribe(observer);

    // Now make a dispose action, which needs to
    // 1. If called more than once, do nothing on subsequent calls
    // 2. Otherwise, call "disposer" to unsubscribe the observable
    // 3. If the reference count hits zero, remove from the dictionary and dispse the observable

    var isDisposed = false;
    return ActionAsDisposable.Create(() => {
      if (Utility.Exchange(ref isDisposed, true)) {
        return;
      }

      // Decrement or remove entry from dictionary
      bool observableRemoved;
      lock (_sync) {
        observableRemoved = dict.DecrementOrRemove(key);
      }

      // Unsubscribe the observer from the observable
      disposer.Dispose();
      if (observableRemoved) {
        // If this was the last Observer to be removed, dispose the Observable
        actualObservable.Dispose();
      }
    });
  }
}
