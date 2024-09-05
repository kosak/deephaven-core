using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn;

/// <summary>
/// Currently this class delegates everything to SessionProviders. When originally
/// envisioned, it was expected to do more. It can possibly be merged with
/// SessionProviders.
/// </summary>
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
