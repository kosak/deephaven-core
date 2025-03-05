﻿using Deephaven.DheClient.Session;
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
    where TKey : notnull
    where TObservable : IObservable<T> {
    TObservable actualObservable;
    lock (_sync) {
      _ = dict.AddOrIncrement(key, candidateObservable, out actualObservable);
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
      lock (_sync) {
        _ = dict.DecrementOrRemove(key);
      }

      // Unsubscribe the observer from the observable
      disposer.Dispose();
    });
  }

  public void SetDefaultEndpointId(string? defaultEndpointId) {
    lock (_sync) {
      _defaultEndpointId = defaultEndpointId;
      _defaultEndpointSelectionObservers.OnNext(_defaultEndpointId);
    }
  }
}
