using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class CoreClientProvider :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObservable<StatusOr<Client>> {
  private const string UnsetClientText = "[Not Connected]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly SequentialExecutor _executor = new();
  private Action? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private KeptAlive<StatusOr<Client>> _client;
  private readonly ObserverContainer<StatusOr<Client>> _observers;
  private readonly VersionTracker _versionTracker = new();

  public CoreClientProvider(StateManager stateManager, EndpointId endpointId, Action onDispose) {
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
    _endpointId = endpointId;
    _onDispose = onDispose;
  }

  public void Init() {
    _upstreamSubscriptionDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
  }

  /// <summary>
  /// Subscribe to Core client changes
  /// </summary>
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

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    if (_workerThread.EnqueueOrNop(() => OnNext(credentials))) {
      return;
    }

    DisposeClientState();

    if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
      _observers.SetAndSendStatus(ref _client, status);
      return;
    }

    _ = cbase.AcceptVisitor(
      core => {
        _observers.SetAndSendStatus(ref _client, "Trying to connect");
        var cookie = _versionTracker.SetNewVersion();
        Utility.RunInBackground(() => CreateClientInSeparateThread(core, cookie));
        return Unit.Instance;
      },
      _ => {
        // We are a Core entity but we are getting credentials for CorePlus
        _observers.SetAndSendStatus(ref _client, "Enterprise Core+ requires a PQ to be specified\"");
        return Unit.Instance;
      });
  }

  private void CreateClientInSeparateThread(CoreEndpointConfig config, VersionTrackerCookie versionCookie) {
    Client? client = null;
    StatusOr<Client> result;
    try {
      client = EndpointFactory.ConnectToCore(config);
      result = StatusOr<Client>.OfValue(client);
    } catch (Exception ex) {
      result = StatusOr<Client>.OfStatus(ex.Message);
    }

    // Some time has passed. It's possible that the VersionTracker has been reset
    // with a newer version. If so, we should throw away our work and leave.
    if (!versionCookie.IsCurrent) {
      client?.Dispose();
      return;
    }

    // Our results are valid. Keep them and tell everyone about it (on the worker thread).
    _workerThread.EnqueueOrRun(() => _observers.SetAndSend(ref _client, result));
  }

  private void DisposeClientState() {
    if (_workerThread.EnqueueOrNop(DisposeClientState)) {
      return;
    }

    _ = _client.GetValueOrStatus(out var oldClient, out _);
    _observers.SetAndSendStatus(ref _client, "Disposing Client");

    if (oldClient != null) {
      Utility.RunInBackground(oldClient.Dispose);
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
