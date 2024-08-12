using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.Operations;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.Util;
using ExcelDna.Integration;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableManager {
  private readonly Dictionary<string, ConnectionProvider> _connectionProviderCollection = new();

  public IDisposable Subscribe(FilteredTableDescriptor descriptor, IObserver<StatusOr<TableHandle>> observer) {
    ConnectionProvider cp;
    lock (_sync) {
      cp = _connectionProviderCollection.LookupOrCreate(descriptor.ConnectionId,
        () => new ConnectionProvider());
    }

    var mco = new MyConnectionObserver(descriptor, observer);
    return cp.Subscribe(mco);
  }
}

internal class Credentials {

}

internal class SessionProvider : IObservable<StatusOr<EitherSession>>, IObserver<Credentials> {
  private Credentials? _credentials;
  private Connection? _connection;

  public IDisposable Subscribe(IObserver<StatusOr<Connection>> observer) {
    InvokeThread666(() => {
      if (_connection == null) {
        observer.OnNext(StatusOr.Of("Not connected"));
      } else {
        observer.OnNext(StatusOr.Of(_connection));
      }

      _collection.Add(observer, out var isFirst);

      if (isFirst) {
        _operationManager.Register(_tableOperation);
      }
    });

    return new ActionAsDisposable(() => RemoveObserver(observer));
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

        _observerCollection.MessageAll($"Connecting to {}");
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



internal class MySessionObserver : IObserver<StatusOr<EitherSession>>, IObserver<StatusOr<Client>> {
  private EitherSession? _eitherSession;

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
