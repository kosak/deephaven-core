﻿using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn;

public class StateManager {
  public readonly WorkerThread WorkerThread = WorkerThread.Create();
  private readonly SessionProviders _sessionProviders;

  public StateManager() {
    _sessionProviders = new SessionProviders(WorkerThread);
  }

  public IDisposable SubscribeToSessions(IObserver<AddOrRemove<EndpointId>> observer) {
    return _sessionProviders.Subscribe(observer);
  }

  public IDisposable SubscribeToSession(EndpointId endpointId, IObserver<StatusOr<SessionBase>> observer) {
    return _sessionProviders.SubscribeToSession(endpointId, observer);
  }

  public IDisposable SubscribeToCredentials(EndpointId endpointId, IObserver<StatusOr<CredentialsBase>> observer) {
    return _sessionProviders.SubscribeToCredentials(endpointId, observer);
  }

  public IDisposable SubscribeToDefaultSession(IObserver<StatusOr<SessionBase>> observer) {
    return _sessionProviders.SubscribeToDefaultSession(observer);
  }

  public IDisposable SubscribeToDefaultCredentials(IObserver<StatusOr<CredentialsBase>> observer) {
    return _sessionProviders.SubscribeToDefaultCredentials(observer);
  }

  public IDisposable SubscribeToTableTriple(TableTriple descriptor, string filter,
    IObserver<StatusOr<TableHandle>> observer) {
    return _sessionProviders.SubscribeToTableTriple(descriptor, filter, observer);
  }

  public void SetCredentials(CredentialsBase credentials) {
    _sessionProviders.SetCredentials(credentials);
  }

  public void SetDefaultCredentials(CredentialsBase credentials) {
    _sessionProviders.SetDefaultCredentials(credentials);
  }

  public void Reconnect(EndpointId id) {
    _sessionProviders.Reconnect(id);
  }

  public void SwitchOnEmpty(EndpointId id, Action onEmpty, Action onNotEmpty) {
    _sessionProviders.SwitchOnEmpty(id, onEmpty, onNotEmpty);
  }
}


using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SessionProviders(WorkerThread workerThread) : IObservable<AddOrRemove<EndpointId>> {
  private readonly Dictionary<EndpointId, SessionProvider> _sessionProviders = new();
  private readonly Dictionary<PersistentQueryKey, PersistentQueryProvider> _persistentQueryProviders = new();
  private readonly Dictionary<TableTriple, TableHandleProvider> _tableHandleProviders = new();
  private readonly Dictionary<FilteredTableProviderKey, FilteredTableProvider> _filteredTableProviders = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _endpointsObservers = new();

  public IDisposable Subscribe(IObserver<AddOrRemove<EndpointId>> observer) {
    workerThread.Invoke(() => {
      _endpointsObservers.Add(observer, out _);

      // Give this observer the current set of endpoint ids.
      var keys = _providerMap.Keys.ToArray();
      foreach (var endpointId in keys) {
        observer.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
      }
    });

    return workerThread.InvokeWhenDisposed(() => {
      _endpointsObservers.Remove(observer, out _);
    });
  }

  public IDisposable SubscribeToSession(EndpointId id, IObserver<StatusOr<SessionBase>> observer) {
    IDisposable? disposable = null;
    var mapEntryDisposer = LookupOrCreateSessionProvider(id,
      sp => disposable = sp.Subscribe(observer),
      _endpointsObservers.OnNext(AddOrRemove<EndpointId>.OfRemove(id)));

    return workerThread.InvokeWhenDisposed(() => {
      Utility.Exchange(ref disposable, null)?.Dispose();
      Utility.Exchange(ref mapEntryDisposer, null)?.Dispose();
    });
  }

  public IDisposable SubscribeToCredentials(EndpointId id, IObserver<StatusOr<CredentialsBase>> observer) {
    IDisposable? disposable = null;
    var mapEntryDisposer = LookupOrCreateSessionProvider(id,
      sp => disposable = sp.Subscribe(observer),
      _endpointsObservers.OnNext(AddOrRemove<EndpointId>.OfRemove(id)));

    return workerThread.InvokeWhenDisposed(() => {
      Utility.Exchange(ref disposable, null)?.Dispose();
      Utility.Exchange(ref mapEntryDisposer, null)?.Dispose();
    });
  }

  public IDisposable SubscribeToDefaultSessionWRONG(IObserver<StatusOr<SessionBase>> observer) {
    IDisposable? disposable = null;
    workerThread.Invoke(() => {
      disposable = _defaultProvider.Subscribe(observer);
    });

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  public IDisposable SubscribeToDefaultCredentialsWRONG(IObserver<StatusOr<CredentialsBase>> observer) {
    IDisposable? disposable = null;
    workerThread.Invoke(() => {
      disposable = _defaultProvider.Subscribe(observer);
    });

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  public IDisposable SubscribeToPq(EndpointId? endpoint, PersistentQueryId? pqId,
    IObserver<StatusOr<Client>> observer) {
    IDisposable? disposable = null;
    var mapEntryDisposer = LookupOrCreatePqProvider(endpoint, pqId,
      thp => disposable = thp.Subscribe(observer), null);

    return workerThread.InvokeWhenDisposed(() => {
      Utility.Exchange(ref disposable, null)?.Dispose();
      Utility.Exchange(ref mapEntryDisposer, null)?.Dispose();
    });
  }

  public IDisposable SubscribeToTableTriple(TableTriple descriptor,
    IObserver<StatusOr<TableHandle>> observer) {
    // There is a chain with multiple elements:
    //
    // 1. Make a TableHandleProvider
    // 2. Make a ClientProvider
    // 3. Subscribe the ClientProvider to either the session provider named by the endpoint id
    //    or to the default session provider
    // 4. Subscribe the TableHandleProvider to the ClientProvider
    // 4. Subscribe our observer to the TableHandleProvider
    // 5. Return a dispose action that disposes all the needfuls.

    var thp = new TableHandleProvider(WorkerThread, descriptor, filter);
    var cp = new ClientProvider(WorkerThread, descriptor);

    var disposer1 = descriptor.EndpointId == null
      ? SubscribeToDefaultSession(cp)
      : SubscribeToSession(descriptor.EndpointId, cp);
    var disposer2 = cp.Subscribe(thp);
    var disposer3 = thp.Subscribe(observer);

    // The disposer for this needs to dispose both "inner" disposers.
    return ActionAsDisposable.Create(() => {
      // TODO(kosak): probably don't need to be on the worker thread here
      WorkerThread.Invoke(() => {
        var temp1 = Utility.Exchange(ref disposer1, null);
        var temp2 = Utility.Exchange(ref disposer2, null);
        var temp3 = Utility.Exchange(ref disposer3, null);
        temp3?.Dispose();
        temp2?.Dispose();
        temp1?.Dispose();
      });
    });
  }

  public IDisposable SubscribeToTableTriple(TableTriple descriptor,
    IObserver<StatusOr<TableHandle>> observer) {

    IDisposable? disposable = null;
    var mapEntryDisposer = LookupOrCreateTableProvider(descriptor,
      thp => disposable = thp.Subscribe(observer), null);

    return workerThread.InvokeWhenDisposed(() => {
      Utility.Exchange(ref disposable, null)?.Dispose();
      Utility.Exchange(ref mapEntryDisposer, null)?.Dispose();
    });
    // // There is a chain with multiple elements:
    // //
    // // 1. Make a TableHandleProvider
    // // 2. Make a ClientProvider
    // // 3. Subscribe the ClientProvider to either the session provider named by the endpoint id
    // //    or to the default session provider
    // // 4. Subscribe the TableHandleProvider to the ClientProvider
    // // 4. Subscribe our observer to the TableHandleProvider
    // // 5. Return a dispose action that disposes all the needfuls.
    //
    // var thp = new TableHandleProvider(WorkerThread, descriptor, filter);
    // var cp = new ClientProvider(WorkerThread, descriptor);
    //
    // var disposer1 = descriptor.EndpointId == null
    //   ? SubscribeToDefaultSession(cp)
    //   : SubscribeToSession(descriptor.EndpointId, cp);
    // var disposer2 = cp.Subscribe(thp);
    // var disposer3 = thp.Subscribe(observer);
    //
    // // The disposer for this needs to dispose both "inner" disposers.
    // return ActionAsDisposable.Create(() => {
    //   // TODO(kosak): probably don't need to be on the worker thread here
    //   WorkerThread.Invoke(() => {
    //     var temp1 = Utility.Exchange(ref disposer1, null);
    //     var temp2 = Utility.Exchange(ref disposer2, null);
    //     var temp3 = Utility.Exchange(ref disposer3, null);
    //     temp3?.Dispose();
    //     temp2?.Dispose();
    //     temp1?.Dispose();
    //   });
    // });
  }

  public IDisposable SubscribeToFilteredTableTriple(TableTriple descriptor, string filter,
    IObserver<StatusOr<TableHandle>> observer) {
    if (filter.Length == 0) {
      return SubscribeToTableTriple(descriptor, observer);
    }

    IDisposable? disposable = null;
    var mapEntryDisposer = LookupOrCreateFilteredTableTriple(descriptor, filter,
      thp => disposable = thp.Subscribe(observer), null);

    return workerThread.InvokeWhenDisposed(() => {
      Utility.Exchange(ref disposable, null)?.Dispose();
      Utility.Exchange(ref mapEntryDisposer, null)?.Dispose();
    });
  }


  // IDisposable? disposable = null;
  //   var mapEntryDisposer = LookupOrCreateFilteredTableTriple(key, thp => disposable = thp.Subscribe(observer));
  //
  //   return workerThread.InvokeWhenDisposed(() => {
  //     Utility.Exchange(ref disposable, null)?.Dispose();
  //     Utility.Exchange(ref mapEntryDisposer, null)?.Dispose();
  //   });
  //
  //   // There is a chain with multiple elements:
  //   //
  //   // 1. Make a TableHandleProvider
  //   // 2. Make a ClientProvider
  //   // 3. Subscribe the ClientProvider to either the session provider named by the endpoint id
  //   //    or to the default session provider
  //   // 4. Subscribe the TableHandleProvider to the ClientProvider
  //   // 4. Subscribe our observer to the TableHandleProvider
  //   // 5. Return a dispose action that disposes all the needfuls.
  //
  //   var thp = new TableHandleProvider(WorkerThread, descriptor, filter);
  //   var cp = new ClientProvider(WorkerThread, descriptor);
  //
  //   var disposer1 = descriptor.EndpointId == null
  //     ? SubscribeToDefaultSession(cp)
  //     : SubscribeToSession(descriptor.EndpointId, cp);
  //   var disposer2 = cp.Subscribe(thp);
  //   var disposer3 = thp.Subscribe(observer);
  //
  //   // The disposer for this needs to dispose both "inner" disposers.
  //   return ActionAsDisposable.Create(() => {
  //     // TODO(kosak): probably don't need to be on the worker thread here
  //     WorkerThread.Invoke(() => {
  //       var temp1 = Utility.Exchange(ref disposer1, null);
  //       var temp2 = Utility.Exchange(ref disposer2, null);
  //       var temp3 = Utility.Exchange(ref disposer3, null);
  //       temp3?.Dispose();
  //       temp2?.Dispose();
  //       temp1?.Dispose();
  //     });
  //   });
  // }

  public void SetCredentials(CredentialsBase credentials) {
    ApplyTo(credentials.Id, sp => {
      sp.SetCredentials(credentials);
    });
  }

  public void SetDefaultCredentials(CredentialsBase credentials) {
    ApplyTo(credentials.Id, _defaultProvider.SetParent);
  }

  public void Reconnect(EndpointId id) {
    ApplyTo(id, sp => sp.Reconnect());
  }

  public void SwitchOnEmpty(EndpointId id, Action callerOnEmpty, Action callerOnNotEmpty) {
    if (workerThread.InvokeIfRequired(() => SwitchOnEmpty(id, callerOnEmpty, callerOnNotEmpty))) {
      return;
    }

    if (!_providerMap.TryGetValue(id, out var sp)) {
      // No provider. That's weird. callerOnEmpty I guess
      callerOnEmpty();
      return;
    }

    // Make a wrapped onEmpty that removes stuff from my dictionary and invokes
    // the observer, then calls the caller's onEmpty

    Action? myOnEmpty = null;
    myOnEmpty = () => {
      if (workerThread.InvokeIfRequired(myOnEmpty!)) {
        return;
      }
      _providerMap.Remove(id);
      _endpointsObservers.OnNext(AddOrRemove<EndpointId>.OfRemove(id));
      callerOnEmpty();
    };

    sp.SwitchOnEmpty(myOnEmpty, callerOnNotEmpty);
  }

  public IDisposable LookupAndSubscribeToSession(EndpointId endpointId,
    IObserver<StatusOr<SessionBase>> observer) {
    IDisposable? disposer = null;
    workerThread.Invoke(() => {
      if (!_sessionProviders.TryGetValue(endpointId, out var sp)) {
        sp = SessionProvider.Create(endpointId, this, workerThread,
          () => _sessionProviders.Remove(endpointId));
        _sessionProviders.Add(endpointId, sp);
      }
      disposer = sp.Subscribe(observer);
    });

    return workerThread.InvokeWhenDisposed(() => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable LookupAndSubscribeToCredentials(EndpointId endpointId,
    IObserver<StatusOr<CredentialsBase>> observer) {
    IDisposable? disposer = null;
    workerThread.Invoke(() => {
      if (!_sessionProviders.TryGetValue(endpointId, out var sp)) {
        sp = SessionProvider.Create(endpointId, this, workerThread,
          () => _sessionProviders.Remove(endpointId));
        _sessionProviders.Add(endpointId, sp);
      }
      disposer = sp.Subscribe(observer);
    });

    return workerThread.InvokeWhenDisposed(() => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable LookupAndSubscribeToPq(EndpointId endpointId, PersistentQueryId? pqId,
    IObserver<StatusOr<Client>> observer) {

    IDisposable? disposer = null;
    workerThread.Invoke(() => {
      var key = new PersistentQueryKey(endpointId, pqId);
      if (!_persistentQueryProviders.TryGetValue(key, out var pqp)) {
        pqp = PersistentQueryProvider.Create(endpointId, pqId, this, workerThread,
          () => _persistentQueryProviders.Remove(key));
        _persistentQueryProviders.Add(key, pqp);
      }
      disposer = pqp.Subscribe(observer);
    });

    return workerThread.InvokeWhenDisposed(() => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  public IDisposable LookupAndSubscribeToTableHandleProvider(
    TableTriple descriptor, IObserver<StatusOr<TableHandle>> observer) {

    IDisposable? disposer = null;
    workerThread.Invoke(() => {
      if (!_tableHandleProviders.TryGetValue(descriptor, out var tp)) {
        tp = TableHandleProvider.Create(descriptor, this, workerThread,
          () => _tableHandleProviders.Remove(descriptor));
        _tableHandleProviders.Add(descriptor, tp);
      }
      disposer = tp.Subscribe(observer);
    });

    return workerThread.InvokeWhenDisposed(() => Utility.Exchange(ref disposer, null)?.Dispose());
  }

  private IDisposable LookupAndSubscribeToFilteredTableProvider(
    TableTriple descriptor, string condition, IObserver<StatusOr<TableHandle>> observer) {
    if (condition.Length == 0) {
      // No filter, so just delegate to LookupAndSubscribeToFilteredTableProvider
      return LookupAndSubscribeToTableHandleProvider(descriptor, observer);
    }

    IDisposable? disposer = null;
    workerThread.Invoke(() => {
      var key = new FilteredTableProviderKey(descriptor.EndpointId, descriptor.PersistentQueryId,
        descriptor.TableName, condition);
      if (!_filteredTableProviders.TryGetValue(key, out var ftp)) {
        ftp = FilteredTableProvider.Create(descriptor, condition, this, workerThread,
          () => _filteredTableProviders.Remove(key));
        _filteredTableProviders.Add(key, ftp);
      }
      disposer = ftp.Subscribe(observer);
    });

    return workerThread.InvokeWhenDisposed(() => Utility.Exchange(ref disposer, null)?.Dispose());
  }
}
