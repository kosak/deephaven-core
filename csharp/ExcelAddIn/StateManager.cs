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
  /// <summary>
  /// Endpoint to CoreClientProvider
  /// </summary>
  private readonly Dictionary<string, WrappedProvider<StatusOr<Client>>> _coreClientProviders = new();
  /// <summary>
  /// (Endpoint, PQ Name) to CorePlusClientProvider
  /// </summary>
  private readonly Dictionary<(string, string), WrappedProvider<StatusOr<DndClient>>> _corePlusClientProviders = new();
  private readonly Dictionary<string, WrappedProvider<StatusOr<EndpointConfigBase>>> _endpointConfigProviders = new();
  private readonly Dictionary<string, WrappedProvider<StatusOr<EndpointHealth>>> _endpointHealthProviders = new();
  private readonly Dictionary<TableQuad, WrappedProvider<StatusOr<TableHandle>>> _tableProviders = new();
  private readonly Dictionary<string, WrappedProvider<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>>>
    _persistentQueryDictProviders = new();
  private readonly Dictionary<(string, string), WrappedProvider<StatusOr<PersistentQueryInfoMessage>>>
    _persistentQueryInfoProviders = new();
  private readonly Dictionary<string, WrappedProvider<StatusOr<SessionManager>>> _sessionManagerProviders = new();

  private readonly EndpointDictProvider _endpointDictProvider = new();

  private readonly ObserverContainer<string?> _defaultEndpointSelectionObservers = new();

  private string? _defaultEndpointId = null;

  public IDisposable SubscribeToCoreClient(string endpointId,
    IObserver<StatusOr<Client>> observer) {
    var candidate = new CoreClientProvider(this, endpointId);
    return SubscribeHelper(_coreClientProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToCorePlusClient(string endpointId, string pqName,
    IObserver<StatusOr<DndClient>> observer) {
    var key = (endpointId, pqName);
    var candidate = new CorePlusClientProvider(this, endpointId, pqName);
    return SubscribeHelper(_corePlusClientProviders, key, candidate, observer);
  }

  public IDisposable SubscribeToEndpointConfig(string endpointId,
    IObserver<StatusOr<EndpointConfigBase>> observer) {
    var candidate = new EndpointConfigProvider();
    return SubscribeHelper(_endpointConfigProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToEndpointHealth(string endpointId,
    IObserver<StatusOr<EndpointHealth>> observer) {
    var candidate = new EndpointHealthProvider(this, endpointId);
    return SubscribeHelper(_endpointHealthProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToTable(TableQuad key, IObserver<StatusOr<TableHandle>> observer) {
    IObservable<StatusOr<TableHandle>> candidate;
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

  public IDisposable SubscribeToPersistentQueryDict(string endpointId,
    IObserver<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> observer) {
    var candidate = new PersistentQueryDictProvider(this, endpointId);
    return SubscribeHelper(_persistentQueryDictProviders, endpointId, candidate, observer);
  }

  public IDisposable SubscribeToPersistentQueryInfo(string endpointId, string pqName,
    IObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
    var key = (endpointId, pqName);
    var candidate = new PersistentQueryInfoProvider(this, endpointId, pqName);
    return SubscribeHelper(_persistentQueryInfoProviders, key, candidate, observer);
  }

  public IDisposable SubscribeToSessionManager(string endpoint,
    IObserver<StatusOr<SessionManager>> observer) {
    var candidate = new SessionManagerProvider(this, endpoint);
    return SubscribeHelper(_sessionManagerProviders, endpoint, candidate, observer);
  }

#if false
  public IDisposable SubscribeToEndpointConfigPopulation(IObserver<AddOrRemove<string>> observer) {
    WorkerThread.EnqueueOrRun(() => {
      _endpointConfigPopulationObservers.Add(observer, out _);

      // Give this observer the current set of endpoint ids.
      var keys = _endpointConfigProviders.Keys.ToArray();
      foreach (var endpointId in keys) {
        observer.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
      }
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(
      () => _endpointConfigPopulationObservers.Remove(observer, out _));
  }

  public IDisposable SubscribeToDefaultEndpointSelection(IObserver<EndpointId?> observer) {
    WorkerThread.EnqueueOrRun(() => {
      _defaultEndpointSelectionObservers.Add(observer, out _);
      observer.OnNext(_defaultEndpointId);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(
      () => _defaultEndpointSelectionObservers.Remove(observer, out _));
  }

  public void SetCredentials(EndpointConfigBase config) {
    LookupOrCreateEndpointConfigProvider(config.Id,
      cp => cp.SetCredentials(config));
  }

  public void Reconnect(EndpointId id) {
    // Quick-and-dirty trick for reconnect is to re-send the credentials to the observers.
    LookupOrCreateEndpointConfigProvider(id, cp => cp.Resend());
  }

  public void TryDeleteEndpointConfig(EndpointId id, Action<bool> successOrFailure) {
    if (WorkerThread.EnqueueOrNop(() => TryDeleteEndpointConfig(id, successOrFailure))) {
      return;
    }

    if (!_endpointConfigProviders.TryGetValue(id, out var cp)) {
      successOrFailure(false);
      return;
    }

    if (cp.ObserverCountUnsafe != 0) {
      successOrFailure(false);
      return;
    }

    // success!
    successOrFailure(true);

    // If we are about to delete the config for the default endpoint
    if (id.Equals(_defaultEndpointId)) {
      SetDefaultEndpointId(null);
    }

    _endpointConfigProviders.Remove(id);
    _endpointConfigPopulationObservers.OnNext(AddOrRemove<EndpointId>.OfRemove(id));
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

  private IDisposable SubscribeHelper<TKey, T>(IDictionary<TKey, WrappedProvider<T>> dict,
    TKey key, IObservable<T> candidateObservable, IObserver<T> observer) {
    WrappedProvider<T>? wrapped;
    lock (_sync) {
      if (!dict.TryGetValue(key, out wrapped)) {
        wrapped = new WrappedProvider<T>(_sync, candidateObservable, () => dict.Remove(key));
        dict.Add(key, wrapped);
      } else {
        wrapped.IncrementLocked();
      }
    }
    return wrapped.Subscribe(observer);
  }

  private IDisposable SubscribeHelper2<TKey, T>(ReferenceCountingDict<TKey, IObservable<T>> dict,
    TKey key, IObservable<T> candidateObservable, IObserver<T> observer) {
    IObservable<T>? actualObservable;
    lock (_sync) {
      actualObservable = dict.AddOrIncrement(key, candidateObservable);
    }
    var isDisposed = new Latch();
    return ActionAsDisposable.Create(() => {
      if (!isDisposed.TrySet()) {
        return;
      }
      lock (_sync) {
        if (!dict.DecrementOrRemove(key, out nubbin)) {
          return;
        }
      }
      nubbin?.Dispose();
    });
    return actualObservable.Subscribe(observer);
  }

  private class WrappedProvider<T> : IObservable<T> {
    private readonly object _sharedSync;
    private readonly IObservable<T> _provider;
    private readonly Action _outerCleanupLocked;
    private int _referenceCount = 1;

    public WrappedProvider(object sharedSync, IObservable<T> provider, Action outerCleanupLocked) {
      _sharedSync = sharedSync;
      _provider = provider;
      _outerCleanupLocked = outerCleanupLocked;
    }

    public IDisposable Subscribe(IObserver<T> observer) {
      var providerDisposer = _provider.Subscribe(observer);
      var isDisposed = false;
      return ActionAsDisposable.Create(() => {
        if (Utility.Exchange(ref isDisposed, true)) {
          return;
        }
        var providerNeedsDisposing = false;
        lock (_sharedSync) {
          if (--_referenceCount == 0) {
            _outerCleanupLocked();
            providerNeedsDisposing = true;
          }
        }
        if (providerNeedsDisposing) {
          providerDisposer.Dispose();
        }
      });
    }

    public void IncrementLocked() {
      ++_referenceCount;
    }
  }

  public void SetDefaultEndpointId(string? defaultEndpointId) {
    lock (_sync) {
      _defaultEndpointId = defaultEndpointId;
      _defaultEndpointSelectionObservers.OnNext(_defaultEndpointId);
    }
  }
}
