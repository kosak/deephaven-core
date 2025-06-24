using Deephaven.Dh_NetClient;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Refcounting;
using Deephaven.ExcelAddIn.Util;
using Utility = Deephaven.ExcelAddIn.Util.Utility;

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
  IValueObservable<StatusOr<Client>> {
  private const string UnsetConfigText = "No Config";
  private const string UnsetClientText = "No Community Core Client";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IObservableCallbacks? _upstreamCallbacks = null;
  private readonly StatusOrHolder<EndpointConfigBase> _cachedConfig = new(UnsetConfigText);
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();
  private readonly StatusOrHolder<Client> _client = new(UnsetClientText);

  public CoreClientProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to Core client changes
  /// </summary>
  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<Client>> observer) {
    lock (_sync) {
      _client.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamCallbacks = _stateManager.SubscribeToEndpointConfig(_endpointId, voc);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    lock (_sync) {
      if (!_cachedConfig.GetValueOrStatus(out _, out _)) {
        // Config parent is in error state, so propagate retry to it
        _upstreamCallbacks?.Retry();
      } else {
        OnNextHelper();
      }
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<Client>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamCallbacks);
      _cachedConfig.Replace(UnsetConfigText);
      _client.Replace(UnsetClientText);
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> config, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      _cachedConfig.Replace(config);
      OnNextHelper();
    }
  }

  private void OnNextHelper() {
    // Invalidate any background work that might be running.
    _backgroundTokenSource.Cancel();
    _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

    if (!_cachedConfig.GetValueOrStatus(out var ecb, out var status)) {
      _client.ReplaceAndNotify(status, _observers);
      return;
    }

    _ = ecb.AcceptVisitor(
      empty => {
        var message = $"Need Community Core Config for {empty.Id}";
        _client.ReplaceAndNotify(message, _observers);
        return Unit.Instance;  // have to return something
      },
      core => {
        var progress = StatusOr<Client>.OfTransient("Trying to connect");
        _client.ReplaceAndNotify(progress, _observers);
        var backgroundToken = _backgroundTokenSource.Token;
        Background.Run(() => OnNextBackground(core, backgroundToken));
        return Unit.Instance;  // have to return something
      },
      corePlus => {
        // Error: we are a Core entity but we are getting credentials for CorePlus
        _client.ReplaceAndNotify("Enterprise Core+ requires a PQ to be specified", _observers);
        return Unit.Instance;  // have to return something
      });
  }

  private void OnNextBackground(CoreEndpointConfig config, CancellationToken token) {
    IDisposable? shareDisposer = null;
    StatusOr<Client> result;
    try {
      var client = EndpointFactory.ConnectToCore(config);
      shareDisposer = Repository.Register(client);
      result = client;
    } catch (Exception ex) {
      result = ex.Message;
    }
    using var cleanup = shareDisposer;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _client.ReplaceAndNotify(result, _observers);
    }
  }
}
