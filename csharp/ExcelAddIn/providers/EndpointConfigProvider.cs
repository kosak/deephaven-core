using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider : IObservable<StatusOr<EndpointConfigBase>> {
  private readonly WorkerThread _workerThread;
  private readonly ObserverContainer<StatusOr<EndpointConfigBase>> _observers = new();
  private StatusOr<EndpointConfigBase> _credentials = StatusOr<EndpointConfigBase>.OfStatus("[No Credentials]");

  public EndpointConfigProvider(StateManager stateManager) {
    _workerThread = stateManager.WorkerThread;
  }

  public void Init() {
    // Do nothing
  }

  public IDisposable Subscribe(IObserver<StatusOr<EndpointConfigBase>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_credentials);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => _observers.Remove(observer, out _));
  }

  public void SetCredentials(EndpointConfigBase newConfig) {
    _observers.SetAndSendValue(ref _credentials, newConfig);
  }

  public void Resend() {
    _observers.OnNext(_credentials);
  }

  public int ObserverCountUnsafe => _observers.Count;
}
