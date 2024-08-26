using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DeephavenClient;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Models;

/// <summary>
/// A "Session" is an abstraction meant to represent a Core or Core+ "session".
/// For Core, this means having a valid Client.
/// For Core+, this means having a SessionManager, through which you can subscribe to PQs and get Clients.
/// </summary>

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

