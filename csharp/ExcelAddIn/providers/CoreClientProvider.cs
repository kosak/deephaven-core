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
  private IDisposable? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly object _sync = new();
  private readonly VersionTracker _versionTracker = new();
  private KeptAlive<StatusOr<Client>> _client;
  private readonly ObserverContainer<StatusOr<Client>> _observers;

  public CoreClientProvider(StateManager stateManager, EndpointId endpointId, IDisposable? onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _onDispose = onDispose;
    _observers = new(_executor);
    _client = MakeState(UnsetClientText);
  }

  public void Init() {
  }

  /// <summary>
  /// Subscribe to Core client changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<Client>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _client.Target);
      if (isFirst) {
        _upstreamSubscriptionDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<Client>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      // Do these teardowns synchronously.
      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _onDispose, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(_client.Move());
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        SetStateAndNotifyLocked(MakeState(status));
        return;
      }

      _ = cbase.AcceptVisitor(
        core => {
          SetStateAndNotifyLocked(MakeState("Trying to connect"));
          var cookie = _versionTracker.SetNewVersion();
          Background666.Run(() => OnNextBackground(core, cookie));
          return Unit.Instance;
        },
        _ => {
          // We are a Core entity but we are getting credentials for CorePlus
          SetStateAndNotifyLocked(MakeState("Enterprise Core+ requires a PQ to be specified"));
          return Unit.Instance;
        });
    }
  }

  private void OnNextBackground(CoreEndpointConfig config, VersionTrackerCookie versionCookie) {
    StatusOr<Client> result;
    try {
      var client = EndpointFactory.ConnectToCore(config);
      result = StatusOr<Client>.OfValue(client);
    } catch (Exception ex) {
      result = StatusOr<Client>.OfStatus(ex.Message);
    }
    using var newKeeper = KeepAlive.Register(result);

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        SetStateAndNotifyLocked(newKeeper.Move());
      }
    }
  }

  private static KeptAlive<StatusOr<Client>> MakeState(string status) {
    var state = StatusOr<Client>.OfStatus(status);
    return KeepAlive.Register(state);
  }

  private void SetStateAndNotifyLocked(KeptAlive<StatusOr<Client>> newState) {
    Background666.InvokeDispose(_client);
    _client = newState;
    _observers.OnNext(newState.Target);
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
