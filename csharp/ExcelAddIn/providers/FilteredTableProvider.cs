﻿using System;
using System.Diagnostics;
using System.Net;
using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

public class StateManager {
  public readonly WorkerThread WorkerThread = WorkerThread.Create();
  private readonly EndpointStateProviders _endpointStateProviders;

  public StateManager() {
    _endpointStateProviders = new EndpointStateProviders(WorkerThread);
  }

  public IDisposable SubscribeToEndpoints(IObserver<AddOrRemove<EndpointId>> observer) {
    return _endpointStateProviders.Subscribe(observer);
  }

  public IDisposable SubscribeToEndpoint(EndpointId endpointId, IObserver<EndpointState> observer) {
    return _endpointStateProviders.Subscribe(endpointId, observer);
  }

  public IDisposable SubscribeToTableTriple(TableTriple descriptor,
    string filter,
    IObserver<StatusOr<TableHandle>> observer) {
    var mco = new MyComboObserver(WorkerThread, descriptor, filter, observer);
    return _endpointStateProviders.Subscribe(descriptor.EndpointId, mco);
  }

  public void SetCredentials(EndpointId id, CredentialsBase credentials) {
    _endpointStateProviders.SetCredentials(id, credentials);
  }

  public void Reconnect(EndpointId id) {
    _endpointStateProviders.Reconnect(id);
  }
}

public record AddOrRemove<T>(bool IsAdd, T Value) {
  public static AddOrRemove<T> OfAdd(T value) {
    return new AddOrRemove<T>(true, value);
  }
}

public record EndpointId(string Id) {
  public string HumanReadableString => Id == "" ? "[Default]" : Id;

  public override string ToString() => HumanReadableString;
}

public record PersistentQueryId(string Id);


internal class EndpointStateProviders : IObservable<AddOrRemove<EndpointId>> {
  private readonly WorkerThread _workerThread;

  private readonly Dictionary<EndpointId, EndpointStateProvider> _providerMap = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _endpointsObservers = new();

  public EndpointStateProviders(WorkerThread workerThread) => _workerThread = workerThread;

  public void SetCredentials(EndpointId id, CredentialsBase credentials) {
    ApplyTo(id, ep => ep.SetCredentials(credentials));
  }

  public void Reconnect(EndpointId id) {
    ApplyTo(id, ep => ep.Reconnect());
  }

  public IDisposable Subscribe(IObserver<AddOrRemove<EndpointId>> observer) {
    IDisposable? disposable = null;
    // We need to run this on our worker thread because we want to protect
    // access ot our dictionary.
    _workerThread.Invoke(() => {
      _endpointsObservers.Add(observer, out _);
      // To avoid any further possibility of reentrancy while iterating over the dict,
      // make a copy of the keys
      var keys = _providerMap.Keys.ToArray();
      foreach (var endpointId in keys) {
        observer.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
      }
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  public IDisposable Subscribe(EndpointId id, IObserver<EndpointState> observer) {
    IDisposable? disposable = null;
    ApplyTo(id, ep => disposable = ep.Subscribe(observer));

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  private void ApplyTo(EndpointId id, Action<EndpointStateProvider> action) {
    _workerThread.Invoke(() => {
      if (!_providerMap.TryGetValue(id, out var ep)) {
        ep = new EndpointStateProvider(id, _workerThread);
        _providerMap.Add(id, ep);
        _endpointsObservers.OnNext(AddOrRemove<EndpointId>.OfAdd(id));
      }

      action(ep);
    });
  }
}


public class EndpointState(CredentialsBase? credentials, StatusOr<SessionBase> session) {
  public static EndpointState OfStatus(CredentialsBase? credentials, string status) {
    var s = StatusOr<SessionBase>.OfStatus(status);
    return new EndpointState(credentials, s);
  }

  public static EndpointState OfValue(CredentialsBase credentials, SessionBase sessionBase) {
    var s = StatusOr<SessionBase>.OfValue(sessionBase);
    return new EndpointState(credentials, s);
  }

  public CredentialsBase? Credentials = credentials;
  public StatusOr<SessionBase> Session = session;
}

internal class EndpointStateProvider : IObservable<EndpointState>, IDisposable {
  private readonly EndpointId _endpointId;
  private readonly WorkerThread _workerThread;
  private EndpointState _endpointState = EndpointState.OfStatus(null, "[Disconnected]");
  private readonly ObserverContainer<EndpointState> _observerContainer = new();
  private bool _disposed;

  public EndpointStateProvider(EndpointId endpointId, WorkerThread workerThread) {
    _endpointId = endpointId;
    _workerThread = workerThread;
  }

  public void Dispose() {
    if (_disposed) {
      return;
    }

    _disposed = true;
    // not even sure what to do.... maybe send an "end" to all of my existing observers?
  }

  public IDisposable Subscribe(IObserver<EndpointState> observer) {
    _workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observerContainer.Add(observer, out _);
      observer.OnNext(_endpointState);
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        _observerContainer.Remove(observer, out _);
      });
    });
  }

  public void Reconnect() {
    _workerThread.Invoke(() => {
      if (_endpointState.Credentials == null) {
        return;
      }
      SetCredentials(_endpointState.Credentials);
    });
  }

  public void SetCredentials(CredentialsBase credentials) {
    _workerThread.Invoke(() => {
      try {
        _endpointState = EndpointState.OfStatus(credentials, "Trying to connect");
        _observerContainer.OnNext(_endpointState);

        var sb = SessionBase.Of(credentials, _workerThread);
        _endpointState = EndpointState.OfValue(credentials, sb);
      } catch (Exception ex) {
        _endpointState = EndpointState.OfStatus(credentials, ex.Message);
      }
      _observerContainer.OnNext(_endpointState);
    });
  }
}

public class SessionBase {
  public static SessionBase Of(CredentialsBase credentials, WorkerThread workerThread) {
    return credentials.AcceptVisitor(
      cc => OfCore(cc),
      ccp => (SessionBase)OfCorePlus(ccp, workerThread));
  }

  public static CoreSession OfCore(CoreCredentials credentials) {
    // TODO(kosak): set session type!!!!
    var client = Client.Connect(credentials.ConnectionString, new ClientOptions().SetSessionType("groovy"));
    return new CoreSession(client);
  }

  public static CorePlusSession OfCorePlus(CorePlusCredentials credentials, WorkerThread workerThread) {
    // TODO(kosak): want a better descriptiveName?
    var session = SessionManager.FromUrl("Deephaven Excel", credentials.JsonUrl);
    if (!session.PasswordAuthentication(credentials.User, credentials.Password, credentials.OperateAs)) {
      throw new Exception("Authentication failed");
    }
    return new CorePlusSession(session, workerThread);
  }

  /// <summary>
  /// This is meant to act like a Visitor pattern with lambdas.
  /// </summary>
  public T Visit<T>(Func<CoreSession, T> onCore, Func<CorePlusSession, T> onCorePlus) {
    if (this is CoreSession cs) {
      return onCore(cs);
    }

    if (this is CorePlusSession cps) {
      return onCorePlus(cps);
    }

    throw new Exception($"Unexpected type {GetType().Name}");
  }
}

public class CoreSession(Client client) : SessionBase {
  public readonly Client Client = client;
}

public class CorePlusSession : SessionBase {
  private readonly SessionManager _sessionManager;
  private readonly WorkerThread _workerThread;
  private readonly Dictionary<PersistentQueryId, ClientProvider> _clientProviders = new();

  public CorePlusSession(SessionManager sessionManager, WorkerThread workerThread) {
    _sessionManager = sessionManager;
    _workerThread = workerThread;
  }

  public IDisposable SubscribeToPq(PersistentQueryId persistentQueryId,
    IObserver<StatusOr<Client>> observer) {
    ClientProvider? cp = null;
    IDisposable? disposer = null;

    _workerThread.Invoke(() => {
      if (!_clientProviders.TryGetValue(persistentQueryId, out cp)) {
        cp = new ClientProvider(_workerThread, _sessionManager, persistentQueryId);
        _clientProviders.Add(persistentQueryId, cp);
        cp.SubscriberCount = 1;
      }

      disposer = cp.Subscribe(observer);
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        var old = Utility.Exchange(ref disposer, null);
        // Do nothing if caller Disposes me multiple times.
        if (old == null) {
          return;
        }
        old.Dispose();

        if (--cp!.SubscriberCount != 0) {
          return;
        }

        // Last one! Remove it from the dictionary and shut it down
        _clientProviders.Remove(persistentQueryId);
        cp!.Dispose();
      });
    });
  }
}


internal class MyComboObserver : IObserver<EndpointState>, IObserver<StatusOr<Client>> {
  private readonly WorkerThread _workerThread;
  private readonly TableTriple _descriptor;
  private readonly string _filter;
  private readonly IObserver<StatusOr<TableHandle>> _callerObserver;
  private IDisposable? _pqDisposable = null;
  private TableHandle? _tableHandle = null;

  public MyComboObserver(WorkerThread workerThread, TableTriple descriptor, string filter,
    IObserver<StatusOr<TableHandle>> observer) {
    _workerThread = workerThread;
    _descriptor = descriptor;
    _filter = filter;
    _callerObserver = observer;
  }

  void IObserver<EndpointState>.OnNext(EndpointState es) {
    _workerThread.Invoke(() => {
      try {
        var oldTh = Utility.Exchange(ref _tableHandle, null);
        var oldPq = Utility.Exchange(ref _pqDisposable, null);

        if (oldTh != null) {
          _callerObserver.SendStatus("Disposing TableHandle");
          oldTh.Dispose();
        }

        if (oldPq != null) {
          _callerObserver.SendStatus("Disposing PQ");
          oldPq.Dispose();
        }

        if (!es.Session.TryGetValue(out var sessionBase, out var status)) {
          _callerObserver.SendStatus(status);
          return;
        }

        // Visit needs a return type and value, so we return (object)null
        _ = sessionBase.Visit(coreSession => {
          this.SendValue(coreSession.Client);
          return (object)null;
        }, corePlusSession => {
          _callerObserver.SendStatus($"Subscribing to PQ \"{_descriptor.PersistentQueryId}\"");
          _pqDisposable = corePlusSession.SubscribeToPq(_descriptor.PersistentQueryId, this);
          return null;
        });
      } catch (Exception ex) {
        _callerObserver.OnError(ex);
      }
    });
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<Client>>.OnNext(StatusOr<Client> so) {
    _workerThread.Invoke(() => {
      try {
        var oldTh = Utility.Exchange(ref _tableHandle, null);

        if (oldTh != null) {
          _callerObserver.SendStatus("Disposing TableHandle");
          oldTh.Dispose();
        }

        if (!so.TryGetValue(out var client, out var status)) {
          _callerObserver.SendStatus(status);
          return;
        }

        _callerObserver.SendStatus($"Fetching \"{_descriptor.TableName}\"");

        _tableHandle = client.Manager.FetchTable(_descriptor.TableName);
        if (_filter != "") {
          var temp = _tableHandle;
          _tableHandle = temp.Where(_filter);
          temp.Dispose();
        }

        _callerObserver.SendValue(_tableHandle);
      } catch (Exception ex) {
        _callerObserver.SendStatus(ex.Message);
      }
    });
  }
}

public static class ObserverStatusOr_Extensions {
  public static void SendStatus<T>(this IObserver<StatusOr<T>> observer, string message) {
    var so = StatusOr<T>.OfStatus(message);
    observer.OnNext(so);
  }

  public static void SendValue<T>(this IObserver<StatusOr<T>> observer, T value) {
    var so = StatusOr<T>.OfValue(value);
    observer.OnNext(so);
  }
}
