using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Microsoft.Office.Interop.Excel;

namespace Deephaven.ExcelAddIn.Providers;

internal class CredentialsProvider(WorkerThread workerThread) : IObservable<StatusOr<CredentialsBase>> {
  private StatusOr<CredentialsBase> _credentials = StatusOr<CredentialsBase>.OfStatusUnknown();
  private readonly ObserverContainer<StatusOr<CredentialsBase>> _observers = new();

  public IDisposable Subscribe(IObserver<StatusOr<CredentialsBase>> observer) {
    workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observers.Add(observer, out _);
      observer.OnNext(_credentials);
    });

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        _observers.Remove(observer, out _);
      });
    });
  }

  public void SetCredentials(CredentialsBase credentials) {
    if (workerThread.InvokeIfRequired(() => SetCredentials(credentials))) {
      return;
    }
    _credentials = StatusOr<CredentialsBase>.OfValue(credentials);
    _observers.SetAndSendValue(ref _credentials, credentials);
  }
}
