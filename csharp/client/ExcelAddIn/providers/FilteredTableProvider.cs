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

internal class ConnectionProvider : IObservable<StatusOr<Connection>>, IObserver<Credentials> {
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



internal class MyConnectionObserver : IObserver<StatusOr<EitherSession>> {
  private Connection? _connection;

  public void OnNext(StatusOr<Connection> so) {
    // whatever this is, dispose of old value
    // then...


    if (so.TryGetStatus(out var status)) {
      innerNubbin.OnNext(status);
      return;
    }

    so.Select(out var coreSession, out var corePlusSession);
    if (coreSession != null) {
      _disposeMonster = coreSession.SubscribeToTable(_tableDescriptor, myHateFulInnerListener);
    } else {
      _disposeMonster = corePlusSession.SubscribeToPq(_tableDescriptor.PerQId, myHaterfulOtherListener);
    }
  }
}

internal class PrefilterNubbin : IObserver<StatusOr<TableHandleProvider>> {
  public void OnNext(StatusOr<TableHandle> value) {
    // whatever this is, dispose of old value
    // then...

    if (so.Status != null) {
      innerNubbin.OnNext(so.Status);
      return;
    }

    // yay we have a TableHandle... but we might need to filter it





    throw new NotImplementedException();
  }
}

internal record FilteredTableDescriptor(string ConnectionId, string PersistentQuery, string TableName, string Filter) {
}
