using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

public class StateManager {
  public readonly WorkerThread WorkerThread = WorkerThread.Create();
  private readonly SessionProviders _sessionProviders;
  private readonly CredentialsProviders _credentialsProviders;

  public StateManager() {
    _sessionProviders = new SessionProviders(WorkerThread);
    _credentialsProviders = new CredentialsProviders(WorkerThread);
  }

  public IDisposable SubscribeToSessions(IObserver<AddOrRemove<EndpointId>> observer) {
    return _sessionProviders.Subscribe(observer);
  }

  public IDisposable SubscribeToSession(EndpointId endpointId, IObserver<StatusOr<SessionBase>> observer) {
    return _sessionProviders.Subscribe(endpointId, observer);
  }

  public IDisposable SubscribeToCredentials(EndpointId endpointId, IObserver<StatusOr<CredentialsBase>> observer) {
    return _credentialsProviders.Subscribe(endpointId, observer);
  }

  public IDisposable SubscribeToTableTriple(TableTriple descriptor, string filter,
    IObserver<StatusOr<TableHandle>> observer) {
    // There is a chain with three elements.
    // The observer (argument to this method) will be a subscriber to a TableHandleProvider that we create here.
    // That TableHandleProvider will in turn be a subscriber to a session.

    // So:
    // 1. Make a TableHandleProvider
    // 2. Subscribe it to the session provider
    // 3. Subscribe our observer to it
    // 4. Return a dispose action that disposes both Subscribes

    var thp = new TableHandleProvider(WorkerThread, descriptor, filter);
    var disposer1 = SubscribeToSession(descriptor.EndpointId, thp);
    var disposer2 = thp.Subscribe(observer);

    // The disposer for this needs to dispose both
    return ActionAsDisposable.Create(() => {
      WorkerThread.Invoke(() => {
        var temp1 = Utility.Exchange(ref disposer1, null);
        var temp2 = Utility.Exchange(ref disposer2, null);
        temp2?.Dispose();
        temp1?.Dispose();
      });
    });
  }

  public void SetCredentials(EndpointId id, CredentialsBase credentials) {
    _credentialsProviders.SetCredentials(id, credentials);
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




// public class EndpointState(CredentialsBase? credentials, StatusOr<SessionBase> session) {
//   public static EndpointState OfStatus(CredentialsBase? credentials, string status) {
//     var s = StatusOr<SessionBase>.OfStatus(status);
//     return new EndpointState(credentials, s);
//   }
//
//   public static EndpointState OfValue(CredentialsBase credentials, SessionBase sessionBase) {
//     var s = StatusOr<SessionBase>.OfValue(sessionBase);
//     return new EndpointState(credentials, s);
//   }
//
//   public CredentialsBase? Credentials = credentials;
//   public StatusOr<SessionBase> Session = session;
// }

// internal class EndpointStateProvider : IObservable<EndpointState>, IDisposable {
//   private readonly EndpointId _endpointId;
//   private readonly WorkerThread _workerThread;
//   private EndpointState _endpointState = EndpointState.OfStatus(null, "[Disconnected]");
//   private readonly ObserverContainer<EndpointState> _observerContainer = new();
//   private bool _disposed;
//
//   public EndpointStateProvider(EndpointId endpointId, WorkerThread workerThread) {
//     _endpointId = endpointId;
//     _workerThread = workerThread;
//   }
//
//   public void Dispose() {
//     if (_disposed) {
//       return;
//     }
//
//     _disposed = true;
//     // not even sure what to do.... maybe send an "end" to all of my existing observers?
//   }
//
//   public IDisposable Subscribe(IObserver<EndpointState> observer) {
//     _workerThread.Invoke(() => {
//       // New observer gets added to the collection and then notified of the current status.
//       _observerContainer.Add(observer, out _);
//       observer.OnNext(_endpointState);
//     });
//
//     return new ActionAsDisposable(() => {
//       _workerThread.Invoke(() => {
//         _observerContainer.Remove(observer, out _);
//       });
//     });
//   }
//
//   public void Reconnect() {
//     _workerThread.Invoke(() => {
//       if (_endpointState.Credentials == null) {
//         return;
//       }
//       SetCredentials(_endpointState.Credentials);
//     });
//   }
//
//   public void SetCredentials(CredentialsBase credentials) {
//     _workerThread.Invoke(() => {
//       try {
//         _endpointState = EndpointState.OfStatus(credentials, "Trying to connect");
//         _observerContainer.OnNext(_endpointState);
//
//         var sb = SessionBase.Of(credentials, _workerThread);
//         _endpointState = EndpointState.OfValue(credentials, sb);
//       } catch (Exception ex) {
//         _endpointState = EndpointState.OfStatus(credentials, ex.Message);
//       }
//       _observerContainer.OnNext(_endpointState);
//     });
//   }
// }

