using Deephaven.DeephavenClient.ExcelAddIn.Util;
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

  /// <summary>
  /// The major difference between the credentials providers and the other providers
  /// is that the credential providers don't remove themselves from the map
  /// upon the last dispose of the subscriber. That is, they hang around until we
  /// manually remove them.
  /// </summary>
  public IDisposable SubscribeToCredentials(EndpointId endpointId,
    IObserver<StatusOr<CredentialsBase>> observer) {
    IDisposable? disposer = null;
    WorkerThread.Invoke(() => {
      if (!_credentialsProviders.TryGetValue(endpointId, out var cp)) {
        cp = CredentialsProvider.Create(endpointId, this);
        _credentialsProviders.Add(endpointId, cp);
      }

      disposer = cp.Subscribe(observer);
    });

    return WorkerThread.InvokeWhenDisposed(() =>
      Utility.Exchange(ref disposer, null)?.Dispose());
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

  public IDisposable LookupAndSubscribeToPq(EndpointId endpointId, PersistentQueryId? pqId,
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

    return WorkerThread.InvokeWhenDisposed(() => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable SubscribeToTableHandleProvider(
    TableTriple descriptor, IObserver<StatusOr<TableHandle>> observer) {

    IDisposable? disposer = null;
    WorkerThread.Invoke(() => {
      if (!_tableHandleProviders.TryGetValue(descriptor, out var tp)) {
        tp = TableHandleProvider.Create(descriptor, this,
          () => _tableHandleProviders.Remove(descriptor));
        _tableHandleProviders.Add(descriptor, tp);
      }
      disposer = tp.Subscribe(observer);
    });

    return WorkerThread.InvokeWhenDisposed(() => Utility.Exchange(ref disposer, null)?.Dispose());
  }


  private IDisposable SubscribeToFilteredTableProvider(
    TableTriple descriptor, string condition, IObserver<StatusOr<TableHandle>> observer) {
    if (condition.Length == 0) {
      // No filter, so just delegate to LookupAndSubscribeToFilteredTableProvider
      return SubscribeToTableHandleProvider(descriptor, observer);
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

    return WorkerThread.InvokeWhenDisposed(() => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  // public void SetCredentials(CredentialsBase credentials) {
  //   ApplyTo(credentials.Id, sp => {
  //     sp.SetCredentials(credentials);
  //   });
  // }
  //
  // public void SetDefaultCredentials(CredentialsBase credentials) {
  //   ApplyTo(credentials.Id, _defaultProvider.SetParent);
  // }
  //
  // public void Reconnect(EndpointId id) {
  //   ApplyTo(id, sp => sp.Reconnect());
  // }
}
