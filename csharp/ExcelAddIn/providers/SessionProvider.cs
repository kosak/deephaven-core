using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SessionProvider(WorkerThread workerThread) : IObserver<StatusOr<CredentialsBase>>, IObservable<StatusOr<SessionBase>> {
  private StatusOr<SessionBase> _session = StatusOr<SessionBase>.OfStatus("[no credentials]");
  private readonly ObserverContainer<StatusOr<SessionBase>> _observers = new();

  public IDisposable Subscribe(IObserver<StatusOr<SessionBase>> observer) {
    workerThread.Invoke(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observers.Add(observer, out _);
      observer.OnNext(_session);
    });

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        _observers.Remove(observer, out _);
      });
    });
  }

  public void OnNext(StatusOr<CredentialsBase> credentials) {
    if (workerThread.InvokeIfRequired(() => OnNext(credentials))) {
      return;
    }

    // Dispose existing session
    if (_session.GetValueOrStatus(out var sess, out var status)) {
      sess.Dispose();
    }

    _session = StatusOr<SessionBase>.OfStatus("Trying to connect");
    if (!credentials.GetValueOrStatus(out var creds, out var credStatus)) {
      _session = StatusOr<SessionBase>.OfStatus(credStatus);
    } else {
      _session = MakeSession(creds);
    }

    _observers.OnNext(_session);
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  private StatusOr<SessionBase> MakeSession(CredentialsBase credentials) {
    try {
      var sb = SessionBaseFactory.Create(credentials, workerThread);
      return StatusOr<SessionBase>.OfValue(sb);
    } catch (Exception ex) {
      return StatusOr<SessionBase>.OfStatus(ex.Message);
    }
  }
}
