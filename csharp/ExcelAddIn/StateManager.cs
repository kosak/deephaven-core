using System.Diagnostics;
using System.Net;
using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn;

public class StateManager {
  public readonly WorkerThread WorkerThread = WorkerThread.Create();
  private readonly Dictionary<EndpointId, CredentialsProvider> _credentialsProviders = new();
  private readonly Dictionary<EndpointId, SessionProvider> _sessionProviders = new();
  private readonly Dictionary<PersistentQueryKey, PersistentQueryProvider> _persistentQueryProviders = new();
  private readonly Dictionary<TableTriple, TableHandleProvider> _tableHandleProviders = new();
  private readonly Dictionary<FilteredTableProviderKey, FilteredTableProvider> _filteredTableProviders = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _credentialsPopulationObservers = new();
  private readonly ObserverContainer<EndpointId?> _defaultEndpointSelectionObservers = new();

  private EndpointId? _defaultEndpointId = null;

  public IDisposable SubscribeToCredentialsPopulation(IObserver<AddOrRemove<EndpointId>> observer) {
    WorkerThread.Invoke(() => {
      _credentialsPopulationObservers.Add(observer, out _);

      // Give this observer the current set of endpoint ids.
      var keys = _credentialsProviders.Keys.ToArray();
      foreach (var endpointId in keys) {
        observer.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
      }
    });

    return WorkerThread.InvokeWhenDisposed(
      () => _credentialsPopulationObservers.Remove(observer, out _));
  }

  public IDisposable SubscribeToDefaultEndpointSelection(IObserver<EndpointId?> observer) {
    WorkerThread.Invoke(() => {
      _defaultEndpointSelectionObservers.Add(observer, out _);
      observer.OnNext(_defaultEndpointId);
    });

    return WorkerThread.InvokeWhenDisposed(
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

    return WorkerThread.InvokeWhenDisposed(() =>
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

  private void LookupOrCreateCredentialsProvider(EndpointId endpointId,
    Action<CredentialsProvider> action) {
    if (WorkerThread.InvokeIfRequired(() => LookupOrCreateCredentialsProvider(endpointId, action))) {
      return;
    }
    if (!_credentialsProviders.TryGetValue(endpointId, out var cp)) {
      cp = CredentialsProvider.Create(endpointId, this);
      _credentialsProviders.Add(endpointId, cp);
      _credentialsPopulationObservers.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
    }

    action(cp);
  }

  public IDisposable SubscribeToSession(EndpointId endpointId,
    IObserver<StatusOr<SessionBase>> observer) {
    IDisposable? disposer = null;
    WorkerThread.Invoke(() => {
      if (!_sessionProviders.TryGetValue(endpointId, out var sp)) {
        sp = SessionProvider.Create(endpointId, this, () => _sessionProviders.Remove(endpointId));
        _sessionProviders.Add(endpointId, sp);
      }
      disposer = sp.Subscribe(observer);
    });

    return WorkerThread.InvokeWhenDisposed(() =>
      Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable SubscribeToPersistentQuery(EndpointId endpointId, PersistentQueryId? pqId,
    IObserver<StatusOr<Client>> observer) {

    IDisposable? disposer = null;
    WorkerThread.Invoke(() => {
      var key = new PersistentQueryKey(endpointId, pqId);
      if (!_persistentQueryProviders.TryGetValue(key, out var pqp)) {
        pqp = PersistentQueryProvider.Create(endpointId, pqId, this,
          () => _persistentQueryProviders.Remove(key));
        _persistentQueryProviders.Add(key, pqp);
      }
      disposer = pqp.Subscribe(observer);
    });

    return WorkerThread.InvokeWhenDisposed(
      () => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable SubscribeToTableHandle(
    TableTriple descriptor, IObserver<StatusOr<TableHandle>> observer) {

    IDisposable? disposer = null;
    WorkerThread.Invoke(() => {
      if (!_tableHandleProviders.TryGetValue(descriptor, out var tp)) {
        tp = TableHandleProvider.Create(descriptor, _defaultEndpointId, this,
          () => _tableHandleProviders.Remove(descriptor));
        _tableHandleProviders.Add(descriptor, tp);
      }
      disposer = tp.Subscribe(observer);
    });

    return WorkerThread.InvokeWhenDisposed(
      () => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable SubscribeToFilteredTableHandle(
    TableTriple descriptor, string condition, IObserver<StatusOr<TableHandle>> observer) {
    if (condition.Length == 0) {
      // No filter, so just delegate to LookupAndSubscribeToFilteredTableProvider
      return SubscribeToTableHandle(descriptor, observer);
    }

    IDisposable? disposer = null;
    WorkerThread.Invoke(() => {
      var key = new FilteredTableProviderKey(descriptor.EndpointId, descriptor.PersistentQueryId,
        descriptor.TableName, condition);
      if (!_filteredTableProviders.TryGetValue(key, out var ftp)) {
        ftp = FilteredTableProvider.Create(descriptor, condition, this,
          () => _filteredTableProviders.Remove(key));
        _filteredTableProviders.Add(key, ftp);
      }
      disposer = ftp.Subscribe(observer);
    });

    return WorkerThread.InvokeWhenDisposed(
      () => Utility.Exchange(ref disposer, null)?.Dispose());
  }
  
  public void SetDefaultCredentials(CredentialsBase credentials) {
    Debug.WriteLine("Not setting default credentials");
  }
}
