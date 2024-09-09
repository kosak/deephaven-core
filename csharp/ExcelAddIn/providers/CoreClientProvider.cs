using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deephaven.DeephavenClient;

namespace ExcelAddIn.providers {
  internal class CoreClientProvider {
  }
}



internal class CorePlusSessionProvider : IObserver<StatusOr<CredentialsBase>>, IObservable<StatusOr<SessionBase>> {
  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly EndpointId _endpointId;
  private Action? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private StatusOr<SessionBase> _session = StatusOr<SessionBase>.OfStatus("[Not connected]");
  private readonly ObserverContainer<StatusOr<SessionBase>> _observers = new();
  private readonly VersionTracker _versionTracker = new();

  public CorePlusSessionProvider(StateManager stateManager, EndpointId endpointId, Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _endpointId = endpointId;
    _onDispose = onDispose;
  }

  public void Init() {
    _upstreamSubscriptionDisposer = _stateManager.SubscribeToCredentials(_endpointId, this);
  }

  /// <summary>
  /// Subscribe to session changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<SessionBase>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_session);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeSessionState();
    });
  }

  public void OnNext(StatusOr<CredentialsBase> credentials) {
    if (_workerThread.EnqueueOrNop(() => OnNext(credentials))) {
      return;
    }

    DisposeSessionState();

    if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
      _observers.SetAndSendStatus(ref _session, status);
      return;
    }

    _observers.SetAndSendStatus(ref _session, "Trying to connect");

    var cookie = _versionTracker.SetNewVersion();
    Utility.RunInBackground(() => CreateSessionBaseInSeparateThread(cbase, cookie));
  }

  private void CreateSessionBaseInSeparateThread(CredentialsBase credentials, VersionTrackerCookie versionCookie) {
    SessionBase? sb = null;
    StatusOr<SessionBase> result;
    try {
      // This operation might take some time.
      sb = SessionBaseFactory.Create(credentials, _workerThread);
      result = StatusOr<SessionBase>.OfValue(sb);
    } catch (Exception ex) {
      result = StatusOr<SessionBase>.OfStatus(ex.Message);
    }

    // Some time has passed. It's possible that the VersionTracker has been reset
    // with a newer version. If so, we should throw away our work and leave.
    if (!versionCookie.IsCurrent) {
      sb?.Dispose();
      return;
    }

    // Our results are valid. Keep them and tell everyone about it (on the worker thread).
    _workerThread.EnqueueOrRun(() => _observers.SetAndSend(ref _session, result));
  }

  private void DisposeSessionState() {
    if (_workerThread.EnqueueOrNop(DisposeSessionState)) {
      return;
    }

    _ = _session.GetValueOrStatus(out var oldSession, out _);
    _observers.SetAndSendStatus(ref _session, "Disposing Session");

    if (oldSession != null) {
      Utility.RunInBackground(oldSession.Dispose);
    }
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }
}


internal class PersistentQueryProvider :
  IObserver<StatusOr<SessionBase>>, IObservable<StatusOr<Client>> {

  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId _pqId;
  private Action? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();
  private StatusOr<Client> _client = StatusOr<Client>.OfStatus("[No Client]");
  private Client? _ownedDndClient = null;

  public PersistentQueryProvider(StateManager stateManager,
    EndpointId endpointId, PersistentQueryId pqId, Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _endpointId = endpointId;
    _pqId = pqId;
    _onDispose = onDispose;
  }

  public void Init() {
    _upstreamSubscriptionDisposer = _stateManager.SubscribeToCorePlusSession(_endpointId, this);
  }

  public IDisposable Subscribe(IObserver<StatusOr<Client>> observer) {
    _workerThread.EnqueueOrRun(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_client);
    });

    return _workerThread.EnqueueOrRunWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Invoke();
      DisposeClientState();
    });
  }

  public void OnNext(StatusOr<SessionBase> sessionBase) {
    if (_workerThread.EnqueueOrNop(() => OnNext(sessionBase))) {
      return;
    }

    DisposeClientState();

    // If the new state is just a status message, make that our state and transmit to our observers
    if (!sessionBase.GetValueOrStatus(out var sb, out var status)) {
      _observers.SetAndSendStatus(ref _client, status);
      return;
    }

    // It's a real Session so start fetching it. Also do some validity checking on the PQ id.
    _ = sb.Visit(
      core => {
        var result = _pqId == null
          ? StatusOr<Client>.OfValue(core.Client)
          : StatusOr<Client>.OfStatus("PQ specified, but Community Core cannot connect to a PQ");
        _observers.SetAndSend(ref _client, result);
        return Unit.Instance;
      },
      corePlus => {
        if (_pqId == null) {
          _observers.SetAndSendStatus(ref _client, "Enterprise Core+ requires a PQ to be specified");
          return Unit.Instance;
        }

        _observers.SetAndSendStatus(ref _client, $"Attaching to \"{_pqId}\"");

        try {
          _ownedDndClient = corePlus.SessionManager.ConnectToPqByName(_pqId.Id, false);
          _observers.SetAndSendValue(ref _client, _ownedDndClient);
        } catch (Exception ex) {
          _observers.SetAndSendStatus(ref _client, ex.Message);
        }
        return Unit.Instance;
      });
  }

  private void DisposeClientState() {
    if (_workerThread.EnqueueOrNop(DisposeClientState)) {
      return;
    }

    _observers.SetAndSendStatus(ref _client, "Disposing Client");
    var oldClient = Utility.Exchange(ref _ownedDndClient, null);
    if (oldClient != null) {
      Utility.RunInBackground(oldClient.Dispose);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}

