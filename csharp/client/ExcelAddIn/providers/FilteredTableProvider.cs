using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SessionProviders {
  /// <summary>
  /// Connection Id -> Session Provider
  /// </summary>
  private readonly Dictionary<string, SessionProvider> _sessionProviderCollection = new();
  private readonly WorkerThread _workerThread = new();

  public void SetCredentials(string id, UnifiedCredentials credentials) {
    ApplyTo(id, sp => sp.SetCredentials(credentials));
  }

  public IDisposable Subscribe(string id, IObserver<StatusOr<UnifiedSession>> observer) {
    IDisposable? disposable = null;
    ApplyTo(id, sp => disposable = sp.Subscribe(observer));

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        Util.SetToNull(ref disposable)?.Dispose();
      });
    });
  }

  private void ApplyTo(string id, Action<SessionProvider> action) {
    _workerThread.Invoke(() => {
      if (!_sessionProviderCollection.TryGetValue(id, out var sp)) {
        sp = new SessionProvider(_workerThread, id);
        _sessionProviderCollection.Add(id, sp);
      }

      action(sp);
    });
  }
}

internal class FilteredTableManager {
  private readonly WorkerThread _workerThread;
  private readonly SessionProviders _sessionProviders;

  public IDisposable Subscribe(FilteredTableDescriptor descriptor,
    IObserver<StatusOr<TableHandle>> observer) {
    SessionProvider? sp = null;
    IDisposable? disposer = null;

    var mco = new MyComboObserver(_workerThread, descriptor, observer);
    return _sessionProviders.Subscribe(descriptor.ConnectionId, mco);
  }
}

internal abstract class UnifiedCredentials {
  /// <summary>
  /// This is meant to be a typesafe way (sort of like a Visitor pattern)
  /// that helps the caller cast UnifiedCredentials down to the right type.
  /// If we ever add a third type, we can add it here. This will help us find
  /// all the callers that need to change.
  /// </summary>
  public void Split(out CoreCredentials? coreCredentials, out CorePlusCredentials? corePlusCredentials) {
    coreCredentials = null;
    corePlusCredentials = null;
    if (this is CoreCredentials cc) {
      coreCredentials = cc;
      return;
    }

    if (this is CorePlusCredentials cpc) {
      corePlusCredentials = cpc;
      return;
    }

    throw new Exception($"Unexpected type {GetType().Name}");
  }
}

internal sealed class CoreCredentials : UnifiedCredentials {
  public readonly string ConnectionString;
}

internal sealed class CorePlusCredentials : UnifiedCredentials {
  public readonly string JsonUrl;
}


internal class SessionProvider : IObservable<StatusOr<UnifiedSession>>, IDisposable {
  private readonly WorkerThread _workerThread;
  private readonly string _connectionId;
  private UnifiedCredentials? _unifiedCredentials = null;
  private UnifiedSession? _unifiedSession = null;
  private readonly ObserverContainer<StatusOr<UnifiedSession>> _observerContainer = new();
  private bool _disposed;

  public SessionProvider(WorkerThread workerThread, string connectionId) {
    _workerThread = workerThread;
    _connectionId = connectionId;
  }

  public void Dispose() {
    if (_disposed) {
      return;
    }

    _disposed = true;
    // not even sure what to do.... maybe send an "end" to all of my existing observers?
  }

  public IDisposable Subscribe(IObserver<StatusOr<UnifiedSession>> observer) {
    _workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observerContainer.Add(observer, out _);

      if (_unifiedSession == null) {
        observer.SendStatus("Not connected");
      } else {
        observer.SendValue(_unifiedSession);
      }
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
        _unifiedSession = null;
        _observerContainer.SendStatusAll($"Trying to connect to {_connectionId}");

        _unifiedSession = UnifiedSession.Of(credentials);
        _observerContainer.SendValueAll(_unifiedSession);
      } catch (Exception ex) {
        _observerContainer.OnErrorAll(ex);
      }
    });
  }
}

internal class UnifiedSession {
  public static UnifiedSession Of(UnifiedCredentials credentials) {
    credentials.Split(out var coreCredentials, out var corePlusCredentials);
    return coreCredentials != null ? OfCore(coreCredentials) : OfCorePlus(corePlusCredentials!);
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

internal class CoreSession(Client client) : UnifiedSession {
  public readonly Client Client = client;
}

internal class CorePlusSession : UnifiedSession {
  private readonly WorkerThread _workerThread;
  private readonly SessionManager _sessionManager;

  /// <summary>
  /// Persistent Query ID -> ClientProvider
  /// </summary>
  private readonly Dictionary<string, ClientProvider> _clientProviders = new();

  public CorePlusSession(SessionManager sessionManager) {
    _sessionManager = sessionManager;
  }

  public IDisposable SubscribeToPq(string persistentQueryId, IObserver<StatusOr<Client>> observer) {
    ClientProvider? cp = null;
    IDisposable? disposer = null;

    _workerThread.Invoke(() => {
      if (!_clientProviders.TryGetValue(persistentQueryId, out cp)) {
        cp = new ClientProvider(_workerThread, persistentQueryId);
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
  private Client? _client = null;
  public int SubscriberCount = 0;

  public IDisposable Subscribe(IObserver<StatusOr<Client>> observer) {
    _workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observers.Add(observer, out _);

      if (_client == null) {
        observer.SendStatus("Not connected to PQ \"{blah}\"");
        return;
      }

      observer.SendValue(_client);
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        _observers.Remove(observer, out _);
      });
    });
  }

  public void Dispose() {
    throw new NotImplementedException();
  }
}

internal class MyComboObserver : IObserver<StatusOr<UnifiedSession>>, IObserver<StatusOr<Client>> {
  private readonly WorkerThread _workerThread;
  private readonly FilteredTableDescriptor _descriptor;
  private readonly IObserver<StatusOr<TableHandle>> _callerObserver;
  private IDisposable? _pqDisposable = null;
  private TableHandle? _tableHandle = null;

  public MyComboObserver(WorkerThread workerThread, FilteredTableDescriptor descriptor,
    IObserver<StatusOr<TableHandle>> observer) {
    _workerThread = workerThread;
    _descriptor = descriptor;
    _callerObserver = observer;
  }

  void IObserver<StatusOr<UnifiedSession>>.OnNext(StatusOr<UnifiedSession> usos) {
    _workerThread.Invoke(() => {
      try {
        Disconnect();
        // MaybeDispose("Table", ref _tableHandle);
        // MaybeDispose("PQ", ref _pqDisposable);

        if (!usos.TryGetValue(out var eitherSession, out var status)) {
          var statusMessage = StatusOr<TableHandle>.OfStatus(status);
          _callerObserver.OnNext(statusMessage);
          return;
        }

        eitherSession.Select(out var coreSession, out var corePlusSession);
        if (coreSession != null) {
          var clientValue = StatusOr<Client>.OfValue(coreSession.Client);
          ((IObserver<StatusOr<Client>>)this).OnNext(clientValue);
          return;
        }

        var pqMessage = $"Subscribing to PQ \"{_descriptor.PersistentQueryId}\"";
        _callerObserver.OnNext(StatusOr<TableHandle>.OfStatus(pqMessage));
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
      MaybeDispose("TableHandle", ref _tableHandle);

      if (!so.TryGetValue(out var client, out var status)) {
        var statusMessage = StatusOr<TableHandle>.OfStatus(status);
        _callerObserver.OnNext(statusMessage);
        return;
      }

      var fetchTableMessage = $"Fetching \"{_descriptor.TableName}\"";
      _callerObserver.OnNext(StatusOr<TableHandle>.OfStatus(fetchTableMessage));

      _tableHandle = client.Manager.FetchTable(_descriptor.TableName);
      if (_descriptor.Filter != "") {
        var temp = _tableHandle;
        _tableHandle = temp.Where(_descriptor.Filter);
        temp.Dispose();
      }

      var tableMessage = StatusOr.OfValue(_tableHandle);
      _callerObserver.OnNext(tableMessage);
    });
  }

  void IObserver<StatusOr<Client>>.OnCompleted() {
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<Client>>.OnError(Exception error) {
    throw new NotImplementedException();
  }

  private void MaybeDispose<T>(string what, ref T? disposable) where T : IDisposable {
    if (!Util.TrySetToNull(ref disposable, out var old)) {
      return;
    }

    var message = StatusOr<TableHandle>.OfStatus($"Disposing {what}");
    _callerObserver.OnNext(message);

    old.Dispose();
  }
}

public class WorkerThread {
  private readonly object _sync = new();
  private readonly Queue<Action> _queue = new();

  public void Invoke(Action a) {
    lock (_sync) {
      _queue.Enqueue(a);
      if (_queue.Count == 1) {
        Monitor.PulseAll(_sync);
      }
    }
  }

  public void Doit() {

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

internal record FilteredTableDescriptor(
  string ConnectionId,
  string PersistentQueryId,
  string TableName,
  string Filter);
