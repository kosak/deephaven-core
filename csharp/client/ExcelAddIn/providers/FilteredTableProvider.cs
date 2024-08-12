using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableManager {
  private readonly object _sync = new();
  private readonly Dictionary<string, SessionProvider> _sessionProviderCollection = new();

  public IDisposable Subscribe(FilteredTableDescriptor descriptor, IObserver<StatusOr<TableHandle>> observer) {
    SessionProvider? sp;
    var needsToSubscribeToCreds = false;
    lock (_sync) {
      if (!_sessionProviderCollection.TryGetValue(descriptor.ConnectionId, out sp)) {
        sp = new SessionProvider();
        _sessionProviderCollection.Add(descriptor.ConnectionId, sp);
        needsToSubscribeToCreds = true;
      }
    }

    if (needsToSubscribeToCreds) {
      someDisposer = _credentialMaster666.Subscribe(sp);
    }

    var mco = new MyComboObserver(descriptor, observer);
    return sp.Subscribe(mco);
  }
}

internal class Credentials {

}

internal class SessionProvider : IObservable<StatusOr<EitherSession>>, IObserver<Credentials> {
  private Credentials? _credentials;
  private EitherSession? _eitherSession;
  private readonly ObserverContainer<StatusOr<EitherSession>> _observerContainer = new();

  public IDisposable Subscribe(IObserver<StatusOr<EitherSession>> observer) {
    InvokeThread666(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observerContainer.Add(observer, out var isFirst);

      if (_eitherSession == null) {
        observer.OnNext(StatusOr<EitherSession>.OfStatus("Not connected"));
      } else {
        observer.OnNext(StatusOr<EitherSession>.OfValue(_eitherSession));
      }
    });

    return new ActionAsDisposable(() => {
      InvokeThread666(() => {
        RemoveObserver(observer);
      });
    });
  }

  void IObserver<Credentials>.OnCompleted() {
    throw new NotImplementedException();
  }

  void IObserver<Credentials>.OnError(Exception error) {
    throw new NotImplementedException();
  }

  void IObserver<Credentials>.OnNext(Credentials value) {
    InvokeThread666(() => {
      try {
        handle_disconnect();

        _observerContainer.MessageAll($"Connecting to {}");
        _credentials = value;
        var sm = SessionManager.FromUrl(_descriptor.jsonUrl);

        _connection = Connection.Of(sm);
        _observerCollection.ValueAll(_connection);
      } catch (Exception ex) {
        _observerCollection.ExceptionAll(ex);
      }
    });
  }
}

internal class EitherSession {
  // from pq to client provider
  private readonly Dictionary<string, ClientProvider> _clientProviderCollection = new();
}



internal class MyComboObserver : IObserver<StatusOr<EitherSession>>, IObserver<StatusOr<Client>> {
  private EitherSession? _eitherSession;

  public MyComboObserver(FilteredTableDescriptor descriptor, IObserver<StatusOr<TableHandle>> observer) {

  }

  public void OnNext(StatusOr<EitherSession> so) {
    // whatever this is, dispose of old value, Session or below
    // then...


    if (so.Status != null) {
      innerNubbin.OnNext(so.Status);
      return;
    }

    so.Select(out var coreSession, out var corePlusSession);
    if (coreSession != null) {
      OnNext(coreSession.Client);
      return;
    }

    _disposeMonster = corePlusSession.SubscribeToPq(_tableDescriptor.PerQId, this);
  }

  public void OnNext(StatusOr<Client> so) {
    // whatever this is, dispose of old value, Client or below

    if (so.Status != null) {
      innerNubbin.OnNext(so.Status);
      return;
    }

    var client = so.Value;
    _tableHandle = client.Manager.FetchTable(_tableDescriptor.TableName);
    if (_tableDescriptor.Filter != "") {
      var temp = _tableHandle;
      _tableHandle = temp.Where(_tableDescriptor.Filter);
      temp.Dispose();
    }

    _zamboniInner.OnNext(_tableHandle);
  }
}

internal record FilteredTableDescriptor(string ConnectionId, string PersistentQuery, string TableName, string Filter) {
}
