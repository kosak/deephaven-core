using System.Diagnostics;
using System.Net;
using Deephaven.DeephavenClient;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn;

public class StateManager {
  public readonly WorkerThread WorkerThread = WorkerThread.Create();
  private readonly Dictionary<EndpointId, CredentialsProvider> _credentialsProviders = new();
  private readonly Dictionary<EndpointId, CoreClientProvider> _coreClientProviders = new();
  private readonly Dictionary<EndpointId, CorePlusSessionProvider> _corePlusSessionProviders = new();
  private readonly Dictionary<PersistentQueryKey, PersistentQueryProvider> _persistentQueryProviders = new();
  private readonly Dictionary<TableQuad, ITableProvider> _tableProviders = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _credentialsPopulationObservers = new();
  private readonly ObserverContainer<EndpointId?> _defaultEndpointSelectionObservers = new();

  private EndpointId? _defaultEndpointId = null;

  public IDisposable SubscribeToCredentialsPopulation(IObserver<AddOrRemove<EndpointId>> observer) {
    WorkerThread.EnqueueOrRun(() => {
      _credentialsPopulationObservers.Add(observer, out _);

      // Give this observer the current set of endpoint ids.
      var keys = _credentialsProviders.Keys.ToArray();
      foreach (var endpointId in keys) {
        observer.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
      }
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(
      () => _credentialsPopulationObservers.Remove(observer, out _));
  }

  public IDisposable SubscribeToDefaultEndpointSelection(IObserver<EndpointId?> observer) {
    WorkerThread.EnqueueOrRun(() => {
      _defaultEndpointSelectionObservers.Add(observer, out _);
      observer.OnNext(_defaultEndpointId);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(
      () => _defaultEndpointSelectionObservers.Remove(observer, out _));
  }

  /// <summary>
  /// The major difference between the credentials providers and the other providers
  /// is that the credential providers don't remove themselves from the map
  /// upon the last dispose of the subscriber. That is, they hang around until we
  /// manually remove them.
  /// </summary>
  public IDisposable SubscribeToCredentials(EndpointId endpointId,
    IObserver<StatusOr<CredentialsBase>> observer) {
    IDisposable? disposer = null;
    LookupOrCreateCredentialsProvider(endpointId,
      cp => disposer = cp.Subscribe(observer));

    return WorkerThread.EnqueueOrRunWhenDisposed(() =>
      Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public void SetCredentials(CredentialsBase credentials) {
    LookupOrCreateCredentialsProvider(credentials.Id,
      cp => cp.SetCredentials(credentials));
  }

  public void Reconnect(EndpointId id) {
    // Quick-and-dirty trick for reconnect is to re-send the credentials to the observers.
    LookupOrCreateCredentialsProvider(id, cp => cp.Resend());
  }

  public void TryDeleteCredentials(EndpointId[] ids, Action<string?[]> failureReasonsAction) {
    if (WorkerThread.EnqueueOrNop(() => TryDeleteCredentials(ids, failureReasonsAction))) {
      return;
    }

    var failureReasons = new List<string?>();
    foreach (var id in ids) {
      if (!_credentialsProviders.TryGetValue(id, out var cp)) {
        failureReasons.Add($"{id} unknown");
        continue;
      }

      if (cp.ObserverCountUnsafe != 0) {
        failureReasons.Add($"{id} is still active");
        continue;
      }

      // success!
      failureReasons.Add(null);

      if (id.Equals(_defaultEndpointId)) {
        SetDefaultEndpointId(null);
      }

      _credentialsProviders.Remove(id);
      _credentialsPopulationObservers.OnNext(AddOrRemove<EndpointId>.OfRemove(id));
    }

    failureReasonsAction(failureReasons.ToArray());
  }

  private void LookupOrCreateCredentialsProvider(EndpointId endpointId,
    Action<CredentialsProvider> action) {
    if (WorkerThread.EnqueueOrNop(() => LookupOrCreateCredentialsProvider(endpointId, action))) {
      return;
    }
    if (!_credentialsProviders.TryGetValue(endpointId, out var cp)) {
      cp = new CredentialsProvider(this);
      _credentialsProviders.Add(endpointId, cp);
      cp.Init();
      _credentialsPopulationObservers.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
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
        sp = new CorePlusSessionProvider(this, endpointId, () => _corePlusSessionProviders.Remove(endpointId));
        _corePlusSessionProviders.Add(endpointId, sp);
        sp.Init();
      }
      disposer = sp.Subscribe(observer);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(() =>
      Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable SubscribeToPersistentQuery(EndpointId endpointId, PersistentQueryId? pqId,
    IObserver<StatusOr<Client>> observer) {

    IDisposable? disposer = null;
    WorkerThread.EnqueueOrRun(() => {
      var key = new PersistentQueryKey(endpointId, pqId);
      if (!_persistentQueryProviders.TryGetValue(key, out var pqp)) {
        pqp = new PersistentQueryProvider(this, endpointId, pqId,
          () => _persistentQueryProviders.Remove(key));
        _persistentQueryProviders.Add(key, pqp);
        pqp.Init();
      }
      disposer = pqp.Subscribe(observer);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(
      () => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable SubscribeToTable(TableQuad key, IObserver<StatusOr<TableHandle>> observer) {
    IDisposable? disposer = null;
    WorkerThread.EnqueueOrRun(() => {
      if (!_tableProviders.TryGetValue(key, out var tp)) {
        Action onDispose = () => _tableProviders.Remove(key);
        if (key.EndpointId == null) {
          tp = new DefaultEndpointTableProvider(this, key.PersistentQueryId, key.TableName, key.Condition,
            onDispose);
        } else if (key.Condition.Length != 0) {
          tp = new FilteredTableProvider(this, key.EndpointId, key.PersistentQueryId, key.TableName,
            key.Condition, onDispose);
        } else {
          tp = new TableProvider(this, key.EndpointId, key.PersistentQueryId, key.TableName, onDispose);
        }
        _tableProviders.Add(key, tp);
        tp.Init();
      }
      disposer = tp.Subscribe(observer);
    });

    return WorkerThread.EnqueueOrRunWhenDisposed(
      () => Utility.Exchange(ref disposer, null)?.Dispose());
  }
  
  public void SetDefaultEndpointId(EndpointId? defaultEndpointId) {
    if (WorkerThread.EnqueueOrNop(() => SetDefaultEndpointId(defaultEndpointId))) {
      return;
    }

    _defaultEndpointId = defaultEndpointId;
    _defaultEndpointSelectionObservers.OnNext(_defaultEndpointId);
  }
}
