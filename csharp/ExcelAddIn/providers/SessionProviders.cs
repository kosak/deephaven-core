﻿using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SessionProviders(WorkerThread workerThread) : IObservable<AddOrRemove<EndpointId>> {
  private readonly DefaultSessionProvider _defaultProvider = new(workerThread);
  private readonly Dictionary<EndpointId, SessionProvider> _providerMap = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _endpointsObservers = new();

  public IDisposable Subscribe(IObserver<AddOrRemove<EndpointId>> observer) {
    // We need to run this on our worker thread because we want to protect
    // access to our dictionary.
    workerThread.Invoke(() => {
      _endpointsObservers.Add(observer, out _);
      // To avoid any further possibility of reentrancy while iterating over the dict,
      // make a copy of the keys
      var keys = _providerMap.Keys.ToArray();
      foreach (var endpointId in keys) {
        observer.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
      }
    });

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        _endpointsObservers.Remove(observer, out _);
      });
    });
  }

  public IDisposable SubscribeToSession(EndpointId id, IObserver<StatusOr<SessionBase>> observer) {
    IDisposable? disposable = null;
    ApplyTo(id, sp => disposable = sp.Subscribe(observer));

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  public IDisposable SubscribeToCredentials(EndpointId id, IObserver<StatusOr<CredentialsBase>> observer) {
    IDisposable? disposable = null;
    ApplyTo(id, sp => disposable = sp.Subscribe(observer));

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  public IDisposable SubscribeToDefaultSession(IObserver<StatusOr<SessionBase>> observer) {
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

  public IDisposable SubscribeToDefaultCredentials(IObserver<StatusOr<CredentialsBase>> observer) {
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

  public IDisposable SubscribeToTableTriple(TableTriple descriptor, string filter,
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


  private void ApplyTo(EndpointId id, Action<SessionProvider> action) {
    if (workerThread.InvokeIfRequired(() => ApplyTo(id, action))) {
      return;
    }

    if (!_providerMap.TryGetValue(id, out var sp)) {
      sp = new SessionProvider(workerThread);
      _providerMap.Add(id, sp);
      _endpointsObservers.OnNext(AddOrRemove<EndpointId>.OfAdd(id));
    }

    action(sp);
  }
}
