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
  private readonly FilteredTableDescriptor _descriptor;
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

  void IObserver<Credentials>.OnNext(Credentials value) {
    InvokeThread666(() => {
      try {
        handle_disconnect();
        _credentials = value;

        var message = StatusOr<EitherSession>.OfStatus($"Connecting to {_descriptor.ConnectionId}");
        _observerContainer.OnNextAll(message);
        var sm = SessionManager.FromUrl(_credentials.JsonUrl);

        _connection = Connection.Of(sm);

        var connectionMessage = StatusOr<EitherSession>.OfValue();
        _observerContainer.OnNextAll(connectionMessage);
      } catch (Exception ex) {
        _observerContainer.OnErrorAll(ex);
      }
    });
  }

  void IObserver<Credentials>.OnCompleted() {
    throw new NotImplementedException();
  }

  void IObserver<Credentials>.OnError(Exception error) {
    throw new NotImplementedException();
  }

}

internal class EitherSession {
  // from pq to client provider
  private readonly Dictionary<string, ClientProvider> _clientProviderCollection = new();
}

internal class MyComboObserver : IObserver<StatusOr<EitherSession>>, IObserver<StatusOr<Client>> {
  private readonly FilteredTableDescriptor _descriptor;
  private readonly IObserver<StatusOr<TableHandle>> _callerObserver;
  private IDisposable? _pqDisposable = null;
  private TableHandle? _tableHandle = null;

  public MyComboObserver(FilteredTableDescriptor descriptor, IObserver<StatusOr<TableHandle>> observer) {
    _descriptor = descriptor;
    _callerObserver = observer;
  }

  void IObserver<StatusOr<EitherSession>>.OnNext(StatusOr<EitherSession> so) {
    InvokeThread666(() => {
      MaybeReleaseTableHandle();
      MaybeReleasePq();
      _callerObserver.OnNext(StatusOr<TableHandle>.OfStatus("Disconnecting from session"));
      _ = Util.TrySetToNull(ref _pqDisposable, out var oldPq);
      _ = Util.TrySetToNull(ref _tableHandle, out var oldTh);
      oldTh?.Dispose();
      oldPq?.Dispose();

      if (!so.TryGetValue(out var eitherSession, out var status)) {
        var statusMessage = StatusOr<TableHandle>.OfStatus(status);
        _callerObserver.OnNext(statusMessage);
        return;
      }

      eitherSession.Select(out var coreSession, out var corePlusSession);
      if (coreSession != null) {
        var clientMessage = StatusOr<Client>.OfValue(coreSession.Client);
        ((IObserver<StatusOr<Client>>)this).OnNext(clientMessage);
        return;
      }

      var pqMessage = $"Subscribing to PQ \"{_descriptor.PersistentQueryId}\"";
      _callerObserver.OnNext(StatusOr<TableHandle>.OfStatus(pqMessage);
      _pqDisposable = corePlusSession.SubscribeToPq(_descriptor.PersistentQueryId, this);
    });
  }

  void IObserver<StatusOr<EitherSession>>.OnCompleted() {
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<EitherSession>>.OnError(Exception error) {
    throw new NotImplementedException();
  }


  void IObserver<StatusOr<Client>>.OnNext(StatusOr<Client> so) {
    InvokeThread666(() => {
      MaybeReleaseTableHandle();

      if (!so.TryGetValue(out var client, out var status)) {
        var statusMessage = StatusOr<TableHandle>.OfStatus(status);
        _callerObserver.OnNext(statusMessage);
        return;
      }

      var fetchTableMessage = $"Fetching \"{_descriptor.TableName}\"";
      _callerObserver.OnNext(StatusOr<TableHandle>.OfStatus(fetchTableMessage));

      _tableHandle = client.Manager.FetchTable(_descriptor.TableName);
      if (_descriptor.Filter != "") {
        var temp = _tableHandle;
        _tableHandle = temp.Where(_descriptor.Filter);
        temp.Dispose();
      }

      var tableMessage = StatusOr.OfValue(_tableHandle);
      _callerObserver.OnNext(tableMessage);
    });
  }

  void IObserver<StatusOr<Client>>.OnCompleted() {
    throw new NotImplementedException();
  }

  void IObserver<StatusOr<Client>>.OnError(Exception error) {
    throw new NotImplementedException();
  }

  private void MaybeReleasePq() {
    if (!Util.TrySetToNull(ref _pqDisposable, out var oldPq)) {
      return;
    }

    oldPq.Dispose();
  }

  private void MaybeReleaseTableHandle() {
    if (!Util.TrySetToNull(ref _tableHandle, out var oldTableHandle)) {
      return;
    }

    oldTableHandle.Dispose();
  }
}

internal record FilteredTableDescriptor(
  string ConnectionId,
  string PersistentQueryId,
  string TableName,
  string Filter);
