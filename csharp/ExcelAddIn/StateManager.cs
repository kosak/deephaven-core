using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Controller;
using System.Windows.Forms;

namespace Deephaven.ExcelAddIn;

public class StateManager {
  private readonly object _sync = new();
  private readonly Dictionary<EndpointId, WrappedProvider<StatusOr<Client>>> _coreClientProviders = new();
  private readonly Dictionary<(EndpointId, string), WrappedProvider<StatusOr<DndClient>>> _corePlusClientProviders = new();
  private readonly Dictionary<EndpointId, WrappedProvider<StatusOr<EndpointConfigBase>>> _endpointConfigProviders = new();
  private readonly Dictionary<EndpointId, WrappedProvider<StatusOr<EndpointHealth>>> _endpointHealthProviders = new();
  private readonly Dictionary<TableQuad, WrappedProvider<StatusOr<TableHandle>>> _tableProviders = new();
  private readonly Dictionary<EndpointId, IObservable<StatusOr<SessionManager>>> _sessionManagerProviders = new();

  private readonly Dictionary<(EndpointId, string), Wrapped<StatusOr<PersistentQueryInfoMessage>>> _pqInfoProviders = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _endpointConfigPopulationObservers = new();
  private readonly ObserverContainer<EndpointId?> _defaultEndpointSelectionObservers = new();

  private EndpointId? _defaultEndpointId = null;

  public IDisposable SubscribeToCoreClient(EndpointId endpointId,
    IObserver<StatusOr<Client>> observer) {

    Func<IObservable<StatusOr<Client>>> factory =
      () => new CoreClientProvider(this, endpointId);
    return SubscribeHelper(endpointId, _coreClientProviders, observer, factory);
  }

  public IDisposable SubscribeToCorePlusClient(EndpointId endpointId, string pqName,
    IObserver<StatusOr<DndClient>> observer) {

    var key = (endpointId, pqName);
    Func<IObservable<StatusOr<DndClient>>> factory =
      () => new CorePlusClientProvider(this, endpointId, pqName);
    return SubscribeHelper(key, _corePlusClientProviders, observer, factory);
  }

  public IDisposable SubscribeToEndpointConfig(EndpointId endpointId,
    IObserver<StatusOr<EndpointConfigBase>> observer) {
    Func<IObservable<StatusOr<EndpointConfigBase>>> factory =
      () => new EndpointConfigProvider();
    return SubscribeHelper(endpointId, _endpointConfigProviders, observer, factory);
  }

  public IDisposable SubscribeToEndpointHealth(EndpointId endpointId,
    IObserver<StatusOr<EndpointHealth>> observer) {
    Func<IObservable<StatusOr<EndpointHealth>>> factory =
      () => new EndpointHealthProvider(this, endpointId);
    return SubscribeHelper(endpointId, _endpointHealthProviders, observer, factory);
  }

  public IDisposable SubscribeToTable(TableQuad key, IObserver<StatusOr<TableHandle>> observer) {
    Func<IObservable<StatusOr<TableHandle>>> factory = () => {
      if (key.EndpointId == null) {
        return new DefaultEndpointTableProvider(this, key.PqName, key.TableName, key.Condition);
      }
      if (key.Condition.Length != 0) {
        return new FilteredTableProvider(this, key.EndpointId, key.PqName, key.TableName,
          key.Condition);
      }
      return new TableProvider(this, key.EndpointId, key.PqName, key.TableName);
    };

    return SubscribeHelper(key, _tableProviders, observer, factory);
  }

  public IDisposable SubscribeToEndpointHealth(EndpointId endpointId,
    IObserver<StatusOr<EndpointHealth>> observer) {

    Func<IObservable<StatusOr<EndpointHealth>>> factory =
      () => new EndpointHealthProvider(this, endpointId);
    return SubscribeHelper(endpointId, _endpointHealthProviders, observer, factory);
  }


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

  


  public IDisposable SubscribeToPqInfo(EndpointId endpointId, string pqName,
    IObserver<StatusOr<PersistentQueryInfoMessage>> observer) {

    var key = (endpointId, pqName);
    var factory = () => new PersistentQueryInfoProvider(this, endpointId, pqName);
    return SubscribeHelper(key, _pqInfoProviders, observer, factory);
  }

  public IDisposable SubscribeToSessionManager(EndpointId endpoint,
    IObserver<StatusOr<SessionManager>> observer) {

    var factory = () => new SessionManagerProvider(this, endpoint);
    return SubscribeHelper(endpoint, _sessionManagerProviders, observer, factory);
  }



  private IDisposable SubscribeHelper<TKey, T>(TKey key,
    IDictionary<TKey, WrappedProvider<T>> dict,
    IObserver<T> observer, Func<IObservable<T>> factory) {
    WrappedProvider<T>? wrapped;
    lock (_sync) {
      if (!dict.TryGetValue(key, out wrapped)) {
        var observable = factory();
        wrapped = new WrappedProvider<T>(_sync, observable, () => dict.Remove(key));
        dict.Add(key, wrapped);
      } else {
        wrapped.IncrementLocked();
      }
    }
    return wrapped.Subscribe(observer);
  }

  private class WrappedProvider<T> : IObservable<T> {
    private readonly object _sharedSync;
    private readonly IObservable<T> _provider;
    private readonly Action _outerCleanupLocked;
    private int _referenceCount = 0;

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
        providerDisposer.Dispose();
        if (providerNeedsDisposing) {
          _provider.Dispose();
        }
      });
    }

    public void IncrementLocked() {
      ++_referenceCount;
    }
  }

  public void SetDefaultEndpointId(EndpointId? defaultEndpointId) {
    lock (_sync) {
      _defaultEndpointId = defaultEndpointId;
      _defaultEndpointSelectionObservers.OnNext(_defaultEndpointId);
    }
  }
}
