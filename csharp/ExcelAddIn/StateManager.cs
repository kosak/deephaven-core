using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using static Deephaven.ExcelAddIn.StateManager;

namespace Deephaven.ExcelAddIn;

public class StateManager {
  private readonly object _sync = new();
  private readonly Dictionary<EndpointId, EndpointConfigProvider> _endpointConfigProviders = new();
  private readonly Dictionary<EndpointId, EndpointHealthProvider> _endpointHealthProviders = new();
  private readonly Dictionary<EndpointId, CoreClientProvider> _coreClientProviders = new();
  private readonly Dictionary<EndpointId, SessionManagerProvider> _corePlusSessionProviders = new();
  private readonly Dictionary<PersistentQueryKey, PersistentQueryInfoProvider> _persistentQueryProviders = new();
  private readonly Dictionary<TableQuad, IObservable<StatusOr<TableHandle>>> _tableProviders = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _endpointConfigPopulationObservers = new();
  private readonly ObserverContainer<EndpointId?> _defaultEndpointSelectionObservers = new();

  private EndpointId? _defaultEndpointId = null;

  public IDisposable SubscribeToEndpointConfigPopulation(IObserver<AddOrRemove<EndpointId>> observer) {
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

  public IDisposable SubscribeToEndpointHealth(EndpointId endpointId,
    IObserver<StatusOr<EndpointHealth>> observer) {
    IDisposable? disposer = null;
    WorkerThread.EnqueueOrRun(() => {
      if (!_endpointHealthProviders.TryGetValue(endpointId, out var ehp)) {
        ehp = new EndpointHealthProvider(this, endpointId, () => _endpointHealthProviders.Remove(endpointId));
        _endpointHealthProviders.Add(endpointId, ehp);
        ehp.Init();
      }
      disposer = ehp.Subscribe(observer);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(() =>
      Utility.Exchange(ref disposer, null)?.Dispose());
  }

  /// <summary>
  /// Note that, unlike the other providers, the connection configs don't remove themselves
  /// from the map upon the last unsubscribe. Rather, they hang around until manually
  /// removed with a TryDeleteCredentials call.
  /// </summary>
  public IDisposable SubscribeToEndpointConfig(EndpointId endpointId,
    IObserver<StatusOr<EndpointConfigBase>> observer) {
    IDisposable? disposer = null;
    LookupOrCreateEndpointConfigProvider(endpointId,
      cp => disposer = cp.Subscribe(observer));

    return WorkerThread.EnqueueOrRunWhenDisposed(() =>
      Utility.Exchange(ref disposer, null)?.Dispose());
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

  public IDisposable SubscribeToCoreClient(EndpointId endpointId,
    IObserver<StatusOr<Client>> observer) {
    IDisposable? disposer = null;
    WorkerThread.EnqueueOrRun(() => {
      if (!_coreClientProviders.TryGetValue(endpointId, out var ccp)) {
        ccp = new CoreClientProvider(this, endpointId, () => _coreClientProviders.Remove(endpointId));
        _coreClientProviders.Add(endpointId, ccp);
        ccp.Init();
      }
      disposer = ccp.Subscribe(observer);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(() =>
      Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable SubscribeToCorePlusSession(EndpointId endpointId,
    IObserver<StatusOr<SessionManager>> observer) {
    IDisposable? disposer = null;
    WorkerThread.EnqueueOrRun(() => {
      if (!_corePlusSessionProviders.TryGetValue(endpointId, out var sp)) {
        sp = new SessionManagerProvider(this, endpointId, () => _corePlusSessionProviders.Remove(endpointId));
        _corePlusSessionProviders.Add(endpointId, sp);
        sp.Init();
      }
      disposer = sp.Subscribe(observer);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(() =>
      Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable SubscribeToPersistentQuery(EndpointId endpointId, PersistentQueryName pqName,
    IObserver<StatusOr<Client>> observer) {

    IDisposable? disposer = null;
    WorkerThread.EnqueueOrRun(() => {
      var key = new PersistentQueryKey(endpointId, pqName);
      if (!_persistentQueryProviders.TryGetValue(key, out var pqp)) {
        pqp = new PersistentQueryProvider(this, endpointId, pqName,
          () => _persistentQueryProviders.Remove(key));
        _persistentQueryProviders.Add(key, pqp);
        pqp.Init();
      }
      disposer = pqp.Subscribe(observer);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(
      () => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public static class Wrapped {
    public static Wrapped<T> Of<T>(IObservable<T> observable, Action onCleanup) {
      return new Wrapped<T>(observable, onCleanup);
    }

  }

  public class Wrapped<T> : IObservable<T>  {
    private readonly IObservable<T> _inner;
    private readonly Action _onCleanup;
    private int _referenceCount = 0;

    public Wrapped(IObservable<T> inner, Action onCleanup) {
      _inner = inner;
      _onCleanup = onCleanup;
    }

    public IDisposable Subscribe(IObserver<T> observer) {
      Interlocked.Increment(ref _referenceCount);
      var innerDisposer = _inner.Subscribe(observer);

      var isDisposed = false;
      return ActionAsDisposable.Create(() => {
        if (Utility.Exchange(ref isDisposed, true)) {
          return;
        }

        if (Interlocked.Decrement(ref _referenceCount) == 0) {
          _onCleanup();
        }
        innerDisposer.Dispose();
      });
    }
  }

  public IDisposable SubscribeToTable(TableQuad key, IObserver<StatusOr<TableHandle>> observer) {
    IObservable<StatusOr<TableHandle>>? wrapped;
    lock (_sync) {
      if (!_tableProviders.TryGetValue(key, out wrapped)) {
        IObservable<StatusOr<TableHandle>> tp;
        if (key.EndpointId == null) {
          tp = new DefaultEndpointTableProvider(this, key.PqName, key.TableName, key.Condition);
        } else if (key.Condition.Length != 0) {
          tp = new FilteredTableProvider(this, key.EndpointId, key.PqName, key.TableName,
            key.Condition);
        } else {
          tp = new TableProvider(this, key.EndpointId, key.PqName, key.TableName);
        }
        wrapped = Wrapped.Of(tp, () => {
          lock (_sync) {
            _tableProviders.Remove(key);
          }
        });
        _tableProviders.Add(key, wrapped);
      }
    }
    return wrapped.Subscribe(observer);
  }

  public void SetDefaultEndpointId(EndpointId? defaultEndpointId) {
    lock (_sync) {
      _defaultEndpointId = defaultEndpointId;
      _defaultEndpointSelectionObservers.OnNext(_defaultEndpointId);
    }
  }
}
