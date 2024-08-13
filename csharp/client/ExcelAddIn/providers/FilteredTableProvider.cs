using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableManager {
  private readonly Dictionary<string, SessionProvider> _sessionProviderCollection = new();
  private readonly WorkerThread _workerThread = new();

  public IDisposable Subscribe(FilteredTableDescriptor descriptor, IObserver<StatusOr<TableHandle>> observer) {
    SessionProvider? sp = null;
    IDisposable? disposer = null;

    _workerThread.Invoke(() => {
      if (!_sessionProviderCollection.TryGetValue(descriptor.ConnectionId, out sp)) {
        sp = new SessionProvider(_workerThread, descriptor);
        _sessionProviderCollection.Add(descriptor.ConnectionId, sp);
        sp.SubscriberCount = 1;
      }

      var mco = new MyComboObserver(_workerThread, descriptor, observer);
      disposer = sp.Subscribe(mco);
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        // Do nothing if caller Disposes me multiple times.
        if (!Util.TryResetToNull(ref disposer, out old)) {
          return;
        }
        old.Dispose();

        if (--sp!.SubscriberCount != 0) {
          return;
        }

        _sessionProviderCollection.Remove(descriptor.ConnectionId);
        sp!.Dispose();
      });
    });
  }
}

internal class UnifiedCredentials {

}

internal class SessionProvider : IObservable<StatusOr<UnifiedSession>>, IObserver<StatusOr<UnifiedCredentials>>, IDisposable {
  private readonly WorkerThread _workerThread;
  private readonly FilteredTableDescriptor _descriptor;
  private UnifiedCredentials? _unifiedCredentials = null;
  private UnifiedSession? _unifiedSession = null;
  private readonly StatusOrObserverContainer<UnifiedSession> _observerContainer = new();
  private IDisposable? _credentialDisposer = null;
  private bool _disposed;

  /// <summary>
  /// Intrusive member, used by FilteredTableManager
  /// </summary>
  public int SubscriberCount = 0;

  public SessionProvider(WorkerThread workerThread, FilteredTableDescriptor descriptor) {
    _workerThread = workerThread;
    _descriptor = descriptor;
    _credentialDisposer = _credentialMaster666.Subscribe(descriptor.ConnectionId, this);
  }

  public void Dispose() {
    if (_disposed) {
      return;
    }

    _disposed = true;
    IDisposable? old2 = null;
    ((old2, _credentialDisposer) = (_credentialDisposer, null)).old2?.Dispose();

    Util.Swap(ref old2, ref _credentialDisposer);
    ;
    if (Util.TryResetToNull(ref _credentialDisposer, out var old)) {
      old.Dispose();
    }
  }

  public IDisposable Subscribe(IObserver<StatusOr<EitherSession>> observer) {
    _workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observerContainer.Add(observer, out _);

      var nextMessage = _eitherSession == null
        ? StatusOr<EitherSession>.OfStatus("Not connected")
        : StatusOr<EitherSession>.OfValue(_eitherSession);
      observer.OnNext(nextMessage);
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        _observerContainer.Remove(observer, out _);
      });
    });
  }

  void IObserver<StatusOr<UnifiedCredentials>>.OnNext(StatusOr<UnifiedCredentials> soc) {
    _workerThread.Invoke(() => {
      try {
        var disconnectMessage = StatusOr<EitherSession>.OfStatus("Disconnected");
        _observerContainer.OnNextAll(disconnectMessage);
        MaybeDispose(ref _eitherSession);

        if (!soc.TryGetValue(out _credentials, out var socMessageText)) {
          var socMessage = StatusOr<EitherSession>.OfStatus(socMessageText);
          _observerContainer.OnNextAll(socMessage);
          return;
        }

        var connectingMessage = StatusOr<EitherSession>.OfStatus($"Connecting to {_descriptor.ConnectionId}");
        _observerContainer.OnNextAll(connectingMessage);

        _unifiedSession = UnifiedSession.Of(_credentials);
        _observerContainer.OnNextAll(_unifiedSession);
      } catch (Exception ex) {
        _observerContainer.OnErrorAll(ex);
      }
    });
  }

  void IObserver<StatusOr<Credentials>>.OnCompleted() {
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<Credentials>>.OnError(Exception error) {
    throw new NotImplementedException();
  }

}

internal class UnifiedSession {
  public static UnifiedSession Of(UnifiedCredentials credentials) {
    credentials.Split(out var coreCredentials, out var corePlusCredentials);
    return coreCredentials != null ? OfCore(coreCredentials) : OfCorePlus(corePlusCredentials);
  }

  public static CoreSession OfCore(CoreCredentials credentials) {
    var client = Client.Connect(credentials.ConnectionString, new ClientOptions());
    return new CoreSession(client);
  }

  public static CorePlusSession OfCorePlus(CorePlusCredentials credentials) {
    // TODO(kosak): want a better descriptive name?
    var session = SessionManager.FromUrl("Deephaven Excel", credentials.JsonUrl);
    return new CorePlusSession(session);
  }
}

internal class CoreSession(Client client) : UnifiedSession {
  public readonly Client Client = client;
}

internal class CorePlusSession : UnifiedSession {
  private readonly SessionManager _sessionManager;
// from pq to client provider
  private readonly Dictionary<string, ClientProvider> _clientProviderCollection = new();

  public CorePlusSession(SessionManager sessionManager) {


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
        _pqDisposable = corePlusSession.SubscribeToPq(_descriptor.PersistentQueryId, this);
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

internal record FilteredTableDescriptor(
  string ConnectionId,
  string PersistentQueryId,
  string TableName,
  string Filter);
