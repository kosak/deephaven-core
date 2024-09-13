using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class CredentialsProvider : IObservable<StatusOr<ConnectionConfigBase>> {
  private readonly WorkerThread _workerThread;
  private readonly ObserverContainer<StatusOr<ConnectionConfigBase>> _observers = new();
  private StatusOr<ConnectionConfigBase> _credentials = StatusOr<ConnectionConfigBase>.OfStatus("[No Credentials]");

  public CredentialsProvider(StateManager stateManager) {
    _workerThread = stateManager.WorkerThread;
  }

  public void Init() {
    // Do nothing
  }

  public IDisposable Subscribe(IObserver<StatusOr<ConnectionConfigBase>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_credentials);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => _observers.Remove(observer, out _));
  }

  public void SetCredentials(ConnectionConfigBase newConfig) {
    _observers.SetAndSendValue(ref _credentials, newConfig);
  }

  public void Resend() {
    _observers.OnNext(_credentials);
  }

  public int ObserverCountUnsafe => _observers.Count;
}
