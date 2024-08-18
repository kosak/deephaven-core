using System;
using System.Diagnostics;
using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class StateManager {
  public readonly WorkerThread WorkerThread = WorkerThread.Create();
  private readonly SessionProviders _sessionProviders;
  private readonly CredentialsProviders _credentialsProviders;

  public StateManager() {
    _sessionProviders = new SessionProviders(WorkerThread);
    _credentialsProviders = new CredentialsProviders(WorkerThread);
  }

  public IDisposable SubscribeToSessions(IObserver<AddOrRemove<SessionId>> observer) {
    return _sessionProviders.Subscribe(observer);
  }

  public IDisposable SubscribeToSession(SessionId sessionId, IObserver<StatusOr<UnifiedSession>> observer) {
    return _sessionProviders.Subscribe(sessionId, observer);
  }

  public IDisposable SubscribeToTriple(TableDescriptor descriptor,
    string filter,
    IObserver<StatusOr<TableHandle>> observer) {
    var mco = new MyComboObserver(WorkerThread, descriptor, filter, observer);
    return _sessionProviders.Subscribe(descriptor.SessionId, mco);
  }

  public IDisposable SubscribeToCredentials(SessionId id, IObserver<UnifiedCredentialsWithEnable> observer) {



  }

  public void SetCredentials(SessionId id, UnifiedCredentials credentials) {
    _sessionProviders.SetCredentials(id, credentials);
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
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _sessionsObservers = new();

  public EndpointStateProviders(WorkerThread workerThread) => _workerThread = workerThread;

  public void SetCredentials(EndpointId id, CredentialsBase credentials) {
    ApplyTo(id, ep => ep.SetCredentials(credentials));
  }

  public IDisposable Subscribe(IObserver<AddOrRemove<SessionId>> observer) {
    IDisposable? disposable = null;
    // We need to run this on our worker thread because we want to protect
    // access ot our dictionary.
    _workerThread.Invoke(() => {
      _sessionsObservers.Add(observer, out _);
      // To avoid any further possibility of reentrancy while iterating over the dict,
      // make a copy of the keys
      var keys = _sessions.Keys.ToArray();
      foreach (var sessionId in keys) {
        observer.OnNext(AddOrRemove<SessionId>.OfAdd(sessionId));
      }
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        Util.SetToNull(ref disposable)?.Dispose();
      });
    });
  }

  public IDisposable Subscribe(SessionId id, IObserver<StatusOr<UnifiedSession>> observer) {
    IDisposable? disposable = null;
    ApplyTo(id, sp => disposable = sp.Subscribe(observer));

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        Util.SetToNull(ref disposable)?.Dispose();
      });
    });
  }

  private void ApplyTo(SessionId id, Action<SessionProvider> action) {
    _workerThread.Invoke(() => {
      if (!_sessions.TryGetValue(id, out var ep)) {
        ep = new EndpointProvider(_workerThread, id);
        _sessions.Add(id, sp);
        _sessionsObservers.OnNextAll(AddOrRemove<SessionId>.OfAdd(id));
      }

      action(sp);
    });
  }
}

public abstract class UnifiedCredentials {
  public static UnifiedCredentials OfCore(string connectionString) {
    return new CoreCredentials(connectionString);
  }

  /// <summary>
  /// This is meant to act like a Visitor pattern with lambdas.
  /// </summary>
  public T Visit<T>(Func<CoreCredentials, T> onCore, Func<CorePlusCredentials, T> onCorePlus) {
    if (this is CoreCredentials cc) {
      return onCore(cc);
    }

    if (this is CorePlusCredentials cpc) {
      return onCorePlus(cpc);
    }

    throw new Exception($"Unexpected type {GetType().Name}");
  }
}

public sealed class CoreCredentials(string connectionString) : UnifiedCredentials {
  public readonly string ConnectionString = connectionString;
}

public sealed class CorePlusCredentials : UnifiedCredentials {
  public readonly string JsonUrl;
}

public class Endpoint(CredentialsBase? credentials,
  StatusOr<SessionBase> session) {
  public CredentialsBase? Credentials = credentials;
  public StatusOr<SessionBase> Session = session;
}

internal class EndpointProvider : IObservable<Endpoint>, IDisposable {
  private readonly WorkerThread _workerThread;
  private readonly SessionId _sessionId;
  private Endpoint? _endpoint = null;
  private readonly ObserverContainer<Endpoint> _observerContainer = new();
  private bool _disposed;

  public EndpointProvider(WorkerThread workerThread, SessionId sessionId) {
    _workerThread = workerThread;
    _sessionId = sessionId;
  }

  public void Dispose() {
    if (_disposed) {
      return;
    }

    _disposed = true;
    // not even sure what to do.... maybe send an "end" to all of my existing observers?
  }

  public IDisposable Subscribe(IObserver<Endpoint> observer) {
    _workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observerContainer.Add(observer, out _);
      observer.OnNext(_unifiedSession);
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        _observerContainer.Remove(observer, out _);
      });
    });
  }

  public void SetCredentials(UnifiedCredentials credentials) {
    _workerThread.Invoke(() => {
      try {
        _unifiedSession = StatusOr<UnifiedSession>.OfStatus("Trying to connect");
        _observerContainer.OnNextAll(_unifiedSession);

        var session = UnifiedSession.Of(credentials);
        _unifiedSession = StatusOr<UnifiedSession>.OfValue(session);
      } catch (Exception ex) {
        _unifiedSession = StatusOr<UnifiedSession>.OfStatus(ex.Message);
      }
      _observerContainer.OnNextAll(_unifiedSession);
    });
  }
}

public class UnifiedSession {
  public static UnifiedSession Of(UnifiedCredentials credentials) {
    return credentials.Visit(cc => (UnifiedSession)OfCore(cc), OfCorePlus);
  }

  public static CoreSession OfCore(CoreCredentials credentials) {
    var client = Client.Connect(credentials.ConnectionString, new ClientOptions());
    return new CoreSession(client);
  }

  public static CorePlusSession OfCorePlus(CorePlusCredentials credentials) {
    // TODO(kosak): want a better descriptiveName?
    var session = SessionManager.FromUrl("Deephaven Excel", credentials.JsonUrl);
    return new CorePlusSession(session);
  }

  /// <summary>
  /// This is meant to be a typesafe way (sort of like a Visitor pattern)
  /// that helps the caller cast UnifiedSession down to the right type.
  /// If we ever add a third type, we can add it here. This will help us find
  /// all the callers that need to change.
  /// </summary>
  public void Select(out CoreSession? coreSession, out CorePlusSession? corePlusSession) {
    coreSession = null;
    corePlusSession = null;
    if (this is CoreSession cs) {
      coreSession = cs;
      return;
    }

    if (this is CorePlusSession cps) {
      corePlusSession = cps;
      return;
    }

    throw new Exception($"Unexpected type {GetType().Name}");

  }
}

public class CoreSession(Client client) : UnifiedSession {
  public readonly Client Client = client;
}

public class CorePlusSession : UnifiedSession {
  private readonly WorkerThread _workerThread;
  private readonly SessionManager _sessionManager;

  /// <summary>
  /// Persistent Query ID -> ClientProvider
  /// </summary>
  private readonly Dictionary<PersistentQueryId, ClientProvider> _clientProviders = new();

  public CorePlusSession(SessionManager sessionManager) {
    _sessionManager = sessionManager;
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
        var old = Util.SetToNull(ref disposer);
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

internal class ClientProvider : IObservable<StatusOr<Client>>, IDisposable {
  private readonly WorkerThread _workerThread;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();
  private StatusOr<Client> _client = StatusOr<Client>.OfStatus("Not connected");
  public int SubscriberCount = 0;

  public ClientProvider(WorkerThread workerThread, SessionManager sessionManager,
    PersistentQueryId persistentQueryId) {
    _workerThread = workerThread;
    _workerThread.Invoke(() => {
      try {
        var dndClient = sessionManager.ConnectToPqByName(persistentQueryId.Id, false);
        _client = StatusOr<Client>.OfValue(dndClient);
      } catch (Exception ex) {
        _client = StatusOr<Client>.OfStatus(ex.Message);
      }
      _observers.OnNextAll(_client);
    });
  }

  public IDisposable Subscribe(IObserver<StatusOr<Client>> observer) {
    _workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observers.Add(observer, out _);
      observer.OnNext(_client);
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        _observers.Remove(observer, out _);
      });
    });
  }

  public void Dispose() {
    _workerThread.Invoke(() => {
      if (_client.TryGetValue(out var c, out _)) {
        _client = StatusOr<Client>.OfStatus("Disposed");
        c.Dispose();
      }
    });
  }
}

internal class MyComboObserver : IObserver<StatusOr<UnifiedSession>>, IObserver<StatusOr<Client>> {
  private readonly WorkerThread _workerThread;
  private readonly TableDescriptor _descriptor;
  private readonly string _filter;
  private readonly IObserver<StatusOr<TableHandle>> _callerObserver;
  private IDisposable? _pqDisposable = null;
  private TableHandle? _tableHandle = null;

  public MyComboObserver(WorkerThread workerThread, TableDescriptor descriptor, string filter,
    IObserver<StatusOr<TableHandle>> observer) {
    _workerThread = workerThread;
    _descriptor = descriptor;
    _filter = filter;
    _callerObserver = observer;
  }

  void IObserver<StatusOr<UnifiedSession>>.OnNext(StatusOr<UnifiedSession> usos) {
    _workerThread.Invoke(() => {
      try {
        if (_tableHandle != null) {
          _callerObserver.SendStatus("Disposing TableHandle");
          Util.SetToNull(ref _tableHandle)?.Dispose();
        }

        if (_pqDisposable != null) {
          _callerObserver.SendStatus("Disposing PQ");
          Util.SetToNull(ref _pqDisposable)?.Dispose();
        }

        if (!usos.TryGetValue(out var eitherSession, out var status)) {
          _callerObserver.SendStatus(status);
          return;
        }

        eitherSession.Select(out var coreSession, out var corePlusSession);
        if (coreSession != null) {
          this.SendValue(coreSession.Client);
          return;
        }

        _callerObserver.SendStatus($"Subscribing to PQ \"{_descriptor.PersistentQueryId}\"");
        _pqDisposable = corePlusSession!.SubscribeToPq(_descriptor.PersistentQueryId, this);
      } catch (Exception ex) {
        _callerObserver.OnError(ex);
      }
    });
  }

  void IObserver<StatusOr<UnifiedSession>>.OnCompleted() {
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<UnifiedSession>>.OnError(Exception error) {
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<Client>>.OnNext(StatusOr<Client> so) {
    _workerThread.Invoke(() => {
      if (_tableHandle != null) {
        _callerObserver.SendStatus("Disposing TableHandle");
        Util.SetToNull(ref _tableHandle)?.Dispose();
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
    });
  }

  void IObserver<StatusOr<Client>>.OnCompleted() {
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<Client>>.OnError(Exception error) {
    throw new NotImplementedException();
  }
}

public class WorkerThread {
  public static WorkerThread Create() {
    var result = new WorkerThread();
    var t = new Thread(result.Doit) { IsBackground = true };
    result._thisThread = t;
    t.Start();
    return result;
  }

  private readonly object _sync = new();
  private readonly Queue<Action> _queue = new();
  private Thread? _thisThread;

  private WorkerThread() {
  }

  public void Invoke(Action action) {
    if (ReferenceEquals(Thread.CurrentThread, _thisThread)) {
      // Can run "action" directly if we're already on our worker thread.
      action();
      return;
    }
    lock (_sync) {
      _queue.Enqueue(action);
      if (_queue.Count == 1) {
        Monitor.PulseAll(_sync);
      }
    }
  }

  public void Doit() {
    while (true) {
      Action? action = null;
      lock (_sync) {
        while (true) {
          if (_queue.Count != 0) {
            action = _queue.Dequeue();
            break;
          }

          Monitor.Wait(_sync);
        }
      }

      try {
        action();
      } catch (Exception ex) {
        Debug.WriteLine($"Swallowing exception {ex}");
      }
    }
  }
}

public static class Util {
  public static T? SetToNull<T>(ref T? item) where T : class {
    var result = item;
    item = null;
    return result;
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

  public static void SendStatusAll<T>(this ObserverContainer<StatusOr<T>> container, string message) {
    var so = StatusOr<T>.OfStatus(message);
    container.OnNextAll(so);
  }

  public static void SendValueAll<T>(this ObserverContainer<StatusOr<T>> container, T value) {
    var so = StatusOr<T>.OfValue(value);
    container.OnNextAll(so);
  }
}

internal record TableDescriptor(
  SessionId SessionId,
  PersistentQueryId PersistentQueryId,
  string TableName) {
  public static bool TryParse(string text, out TableDescriptor result, out string errorText) {
    // 1. table - ("", "", table)
    // 2. connection:table - (connection, "", table)
    // 3. pq/table - ("", pq, table)
    // 4. connection:pq/table - (connection, pq, table)
    var cid = "";
    var pqid = "";
    var tableName = "";
    var colonIndex = text.IndexOf(':');
    if (colonIndex > 0) {
      // cases 2 and 4: pull out the connection, and then reduce to cases 1 and 3
      cid = text.Substring(0, colonIndex);
      text = text.Substring(colonIndex + 1);
    }

    var slashIndex = text.IndexOf('/');
    if (slashIndex > 0) {
      // case 3: pull out the slash, and reduce to case 1
      pqid = text.Substring(0, slashIndex);
      text = text.Substring(slashIndex + 1);
    }

    tableName = text;
    result = new TableDescriptor(new SessionId(cid),
      new PersistentQueryId(pqid), tableName);
    errorText = "";
    // This version never fails to parse, but we leave open the option to do so.
    return true;
  }
}
