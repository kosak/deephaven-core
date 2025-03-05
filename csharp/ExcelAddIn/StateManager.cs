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
  /// EndpointId to CoreClientProvider
  /// </summary>
  private readonly ReferenceCountingDict<string, CoreClientProvider> _coreClientProviders = new();
  /// <summary>
  /// (EndpointId, PQ Name) to CorePlusClientProvider
  /// </summary>
  private readonly ReferenceCountingDict<(string, string), CorePlusClientProvider> _corePlusClientProviders = new();
  /// <summary>
  /// EndpointId to EndpointConfigProvider
  /// </summary>
  private readonly ReferenceCountingDict<string, EndpointConfigProvider> _endpointConfigProviders = new();
  /// <summary>
  /// EndpointId to EndpointHealthProvider
  /// </summary>
  private readonly ReferenceCountingDict<string, EndpointHealthProvider> _endpointHealthProviders = new();
  /// <summary>
  /// EndpointId to (DefaultEndpointTableProvider, FilteredTableProvider, or TableProvider)
  /// </summary>
  private readonly ReferenceCountingDict<TableQuad, IObservable<StatusOr<TableHandle>>> _tableProviders = new();
  /// <summary>
  /// EndpointId to PersistentQueryDictProvider
  /// </summary>
  private readonly ReferenceCountingDict<string, PersistentQueryDictProvider> _persistentQueryDictProviders = new();
  /// <summary>
  /// (EndpointId, PQ Name) to PersistentQueryInfoProvider
  /// </summary>
  private readonly ReferenceCountingDict<(string, string), PersistentQueryInfoProvider> _persistentQueryInfoProviders = new();
  /// <summary>
  /// (EndpointId, PQ Name) to SessionManagerProvider
  /// </summary>
  private readonly ReferenceCountingDict<string, SessionManagerProvider> _sessionManagerProviders = new();

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

  public IDisposable SubscribeToSessionManager(string endpointId,
    IObserver<StatusOr<SessionManager>> observer) {
    var candidate = new SessionManagerProvider(this, endpointId);
    return SubscribeHelper(_sessionManagerProviders, endpointId, candidate, observer);
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

  private IDisposable SubscribeHelper<TKey, TObservable, T>(ReferenceCountingDict<TKey, TObservable> dict,
    TKey key, TObservable candidateObservable, IObserver<T> observer)
    where TObservable : IObservable<T> {
    TObservable actualObservable;
    lock (_sync) {
      actualObservable = dict.AddOrIncrement(key, candidateObservable);
    }

    var disposer = actualObservable.Subscribe(observer);

    var isDisposed = new Latch();
    return ActionAsDisposable.Create(() => {
      if (!isDisposed.TrySet()) {
        return;
      }
      lock (_sync) {
        if (!dict.DecrementOrRemove(key)) {
          return;
        }
      }
      disposer.Dispose();
    });
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
