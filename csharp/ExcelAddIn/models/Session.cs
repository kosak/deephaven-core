using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DeephavenClient;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Models;

/// <summary>
/// A "Session" is an abstraction meant to represent a Core or Core+ "session".
/// For Core, this means having a valid Client.
/// For Core+, this means having a SessionManager, through which you can subscribe to PQs and get Clients.
/// </summary>
public abstract class SessionBase {
  /// <summary>
  /// This is meant to act like a Visitor pattern with lambdas.
  /// </summary>
  public abstract T Visit<T>(Func<CoreSession, T> onCore, Func<CorePlusSession, T> onCorePlus);
}

public class CoreSession(Client client) : SessionBase {
  public readonly Client Client = client;

  public override T Visit<T>(Func<CoreSession, T> onCore, Func<CorePlusSession, T> onCorePlus) {
    return onCore(this);
  }
}

public class CorePlusSession(SessionManager sessionManager, WorkerThread workerThread) : SessionBase {
  private readonly Dictionary<PersistentQueryId, CorePlusClientProvider> _clientProviders = new();

  public override T Visit<T>(Func<CoreSession, T> onCore, Func<CorePlusSession, T> onCorePlus) {
    return onCorePlus(this);
  }

  public IDisposable SubscribeToPq(PersistentQueryId persistentQueryId,
    IObserver<StatusOr<Client>> observer) {
    CorePlusClientProvider? cp = null;
    IDisposable? disposer = null;

    workerThread.Invoke(() => {
      if (!_clientProviders.TryGetValue(persistentQueryId, out cp)) {
        cp = CorePlusClientProvider.Create(workerThread, sessionManager, persistentQueryId);
        _clientProviders.Add(persistentQueryId, cp);
      }

      disposer = cp.Subscribe(observer);
    });

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        var old = Utility.Exchange(ref disposer, null);
        // Do nothing if caller Disposes me multiple times.
        if (old == null) {
          return;
        }
        old.Dispose();

        if (cp!.SubscriberCount != 0) {
          return;
        }

        // Last one! Remove the CorePlusClientProvider from the dictionary and shut it down
        _clientProviders.Remove(persistentQueryId);
        cp!.Dispose();
      });
    });
  }
}

