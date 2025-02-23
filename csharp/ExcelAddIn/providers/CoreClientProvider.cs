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
  private readonly object _sync = new();
  private IDisposable? _onDispose;
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<Client> _client = UnsetClientText;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();

  public CoreClientProvider(StateManager stateManager, EndpointId endpointId, IDisposable? onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _onDispose = onDispose;
  }

  /// <summary>
  /// Subscribe to Core client changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<Client>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _client);
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
        SetStateAndNotifyLocked(status);
        return;
      }

      _ = cbase.AcceptVisitor(
        core => {
          SetStateAndNotifyLocked("Trying to connect");
          var cookie = _versionTracker.SetNewVersion();
          Background666.Run(() => OnNextBackground(core, cookie));
          return Unit.Instance;
        },
        _ => {
          // We are a Core entity but we are getting credentials for CorePlus
          SetStateAndNotifyLocked("Enterprise Core+ requires a PQ to be specified");
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
      result = ex.Message;
    }
    using var cleanup = result;

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        SetStateAndNotifyLocked(result);
      }
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
