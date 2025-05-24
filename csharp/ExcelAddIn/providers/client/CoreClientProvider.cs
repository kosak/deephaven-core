using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
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
  IValueObserverWithCancel<StatusOr<EndpointConfigBase>>,
  IValueObservable<StatusOr<RefCounted<Client>>> {
  private const string UnsetClientText = "[No Community Core Client]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<Client>>> _observers = new();
  private StatusOr<RefCounted<Client>> _client = UnsetClientText;

  public CoreClientProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to Core client changes
  /// </summary>
  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<Client>>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _client, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, voc);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IValueObserver<StatusOr<RefCounted<Client>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _client, UnsetClientText);
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> config, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      if (!config.GetValueOrStatus(out var ecb, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _client, status, _observers);
        return;
      }

      _ = ecb.AcceptVisitor(
        empty => {
          StatusOrUtil.ReplaceAndNotify(ref _client, UnsetClientText, _observers);
          return Unit.Instance;  // have to return something
        },
        core => {
          var progress = StatusOr<RefCounted<Client>>.OfProgress("Trying to connect");
          StatusOrUtil.ReplaceAndNotify(ref _client, progress, _observers);
          var backgroundToken = _backgroundTokenSource.Token;
          Background.Run(() => OnNextBackground(core, backgroundToken));
          return Unit.Instance;  // have to return something
        },
        corePlus => {
          // Error: we are a Core entity but we are getting credentials for CorePlus
          StatusOrUtil.ReplaceAndNotify(ref _client,
            "Enterprise Core+ requires a PQ to be specified", _observers);
          return Unit.Instance;  // have to return something
        });
    }
  }

  private void OnNextBackground(CoreEndpointConfig config, CancellationToken token) {
    RefCounted<Client>? newRef = null;
    StatusOr<RefCounted<Client>> result;
    try {
      var client = EndpointFactory.ConnectToCore(config);
      newRef = RefCounted.Acquire(client);
      result = newRef;
    } catch (Exception ex) {
      result = ex.Message;
    }
    using var cleanup = newRef;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _client, result, _observers);
    }
  }
}
