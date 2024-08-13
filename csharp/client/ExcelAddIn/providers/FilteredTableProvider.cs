using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableManager {
  private readonly Dictionary<string, SessionProvider> _sessionProviderCollection = new();

  public IDisposable Subscribe(FilteredTableDescriptor descriptor, IObserver<StatusOr<TableHandle>> observer) {
    SessionProvider? sp = null;
    IDisposable? disposer = null;

    InvokeThread666(() => {
      if (!_sessionProviderCollection.TryGetValue(descriptor.ConnectionId, out sp)) {
        sp = new SessionProvider(descriptor);
        _sessionProviderCollection.Add(descriptor.ConnectionId, sp);
        sp.SubscriberCount = 1;
        sp.CredentialDisposer = _credentialMaster666.Subscribe(sp);
      }

      var mco = new MyComboObserver(descriptor, observer);
      disposer = sp.Subscribe(mco);
    });

    return new ActionAsDisposable(() => {
      InvokeThread666(() => {
        if (--sp!.SubscriberCount != 0) {
          return;
        }

        _sessionProviderCollection.Remove(descriptor.ConnectionId);
        sp!.CredentialDisposer!.Dispose();
        disposer?.Dispose();
      });
    });
  }
}

internal class Credentials {

}

internal class SessionProvider : IObservable<StatusOr<EitherSession>>, IObserver<Credentials> {
  private readonly FilteredTableDescriptor _descriptor;
  private Credentials? _credentials = null;
  private EitherSession? _eitherSession = null;
  private readonly ObserverContainer<StatusOr<EitherSession>> _observerContainer = new();

  /// <summary>
  /// Intrusive members, used by FilteredTableManager
  /// </summary>
  public int SubscriberCount = 0;

  /// <summary>
  /// Intrusive members, used by FilteredTableManager
  /// </summary>
  public IDisposable? CredentialDisposer = null;

  public SessionProvider(FilteredTableDescriptor descriptor) {
    _descriptor = descriptor;
  }

  public IDisposable Subscribe(IObserver<StatusOr<EitherSession>> observer) {
    InvokeThread666(() => {
      // New observer gets added to the collection and then notified of the current status.
      _observerContainer.Add(observer, out _);

      var nextMessage = _eitherSession == null
        ? StatusOr<EitherSession>.OfStatus("Not connected")
        : StatusOr<EitherSession>.OfValue(_eitherSession);
      observer.OnNext(nextMessage);
    });

    return new ActionAsDisposable(() => {
      InvokeThread666(() => {
        _observerContainer.Remove(observer, out _);
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
      try {
        MaybeDispose("Table", ref _tableHandle);
        MaybeDispose("PQ", ref _pqDisposable);

        if (!so.TryGetValue(out var eitherSession, out var status)) {
          var statusMessage = StatusOr<TableHandle>.OfStatus(status);
          _callerObserver.OnNext(statusMessage);
          return;
        }

        eitherSession.Select(out var coreSession, out var corePlusSession);
        if (coreSession != null) {
          var clientValue = StatusOr<Client>.OfValue(coreSession.Client);
          ((IObserver<StatusOr<Client>>)this).OnNext(clientValue);
          return;
        }

        var pqMessage = $"Subscribing to PQ \"{_descriptor.PersistentQueryId}\"";
        _callerObserver.OnNext(StatusOr<TableHandle>.OfStatus(pqMessage));
        _pqDisposable = corePlusSession.SubscribeToPq(_descriptor.PersistentQueryId, this);
      } catch (Exception ex) {
        _callerObserver.OnError(ex);
      }
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
