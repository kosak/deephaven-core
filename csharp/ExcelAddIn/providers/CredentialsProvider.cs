using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class CredentialsProvider : IObservable<StatusOr<CredentialsBase>> {
  private readonly WorkerThread _workerThread;
  private readonly ObserverContainer<StatusOr<CredentialsBase>> _observers = new();
  private StatusOr<CredentialsBase> _credentials = StatusOr<CredentialsBase>.OfStatus("[No Credentials]");

  public CredentialsProvider(WorkerThread workerThread) {
    _workerThread = workerThread;
  }

  public void Init(StateManager sm) {
    // Do nothing
  }

  public IDisposable Subscribe(IObserver<StatusOr<CredentialsBase>> observer) {
    _workerThread.Invoke(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_credentials);
    });

    return _workerThread.InvokeWhenDisposed(() => _observers.Remove(observer, out _));
  }

  public void SetCredentials(CredentialsBase newCredentials) {
    _observers.SetAndSendValue(ref _credentials, newCredentials);
  }

  public void Resend() {
    _observers.OnNext(_credentials);
  }

  public int ObserverCountUnsafe => _observers.Count;
}
