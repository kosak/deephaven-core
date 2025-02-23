using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/**
 * The job of this class is to observe EndpointConfig notifications for a given EndpointId,
 * and then provide Client notifications. If the EndpointConfig does not refer to a Community
 * Core instance, we notify an error. If it does, we try to connect to that instance in the
 * background. If that connection eventually succeeds, we notify our observers with the
 * corresponding Client object. Otherwise we notify an error.
 */
internal class CoreClientProvider :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObservable<StatusOr<Client>> {
  private const string UnsetClientText = "[Not Connected to Community Core Client]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<Client> _client = UnsetClientText;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();

  public CoreClientProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
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
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }
    lock (_sync) {
      // Do these teardowns synchronously.
      Utility.Exchange(ref _upstreamSubscriptionDisposer, null)?.Dispose();
      // Release our Deephaven resource asynchronously.
      Background666.InvokeDispose(Utility.Exchange(ref _client, UnsetClientText));
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        _observers.SetStateAndNotify(ref _client, status);
        return;
      }

      _ = cbase.AcceptVisitor(
        core => {
          _observers.SetStateAndNotify(ref _client, "Trying to connect");
          var cookie = _versionTracker.SetNewVersion();
          Background666.Run(() => OnNextBackground(core, cookie));
          return Unit.Instance;
        },
        _ => {
          // We are a Core entity but we are getting credentials for CorePlus
          _observers.SetStateAndNotify(ref _client, "Enterprise Core+ requires a PQ to be specified");
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
        _observers.SetStateAndNotify(ref _client, result);
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
