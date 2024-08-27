using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn;

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
