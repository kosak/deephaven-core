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
  IStatusObserver<EndpointConfigBase>,
  IStatusObservable<RefCounted<Client>>,
  IDisposable {
  private const string UnsetClientText = "[No Community Core Client]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly FreshnessSource _freshness;
  private readonly Latch _subscribeDone = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<RefCounted<Client>> _observers = new();
  private StatusOr<RefCounted<Client>> _client = UnsetClientText;

  public CoreClientProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _freshness = new(_sync);
  }

  /// <summary>
  /// Subscribe to Core client changes
  /// </summary>
  public IDisposable Subscribe(IStatusObserver<RefCounted<Client>> observer) {
    lock (_sync) {
      SorUtil.AddObserverAndNotify(_observers, observer, _client, out _);
      if (_subscribeDone.TrySet()) {
        // Subscribe to parent at the first-ever subscribe
        _upstreamDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IStatusObserver<RefCounted<Client>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _upstreamDisposer);
      SorUtil.Replace(ref _client, "[Disposing]");
    }
  }

  public void OnStatus(string status) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Invalidate any background work that might be running.
      _ = _freshness.New();
      SorUtil.ReplaceAndNotify(ref _client, status, _observers);
    }
  }

  public void OnNext(EndpointConfigBase credentials) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Invalidate any background work that might be running.
      var cookie = _freshness.New();

      _ = credentials.AcceptVisitor(
        core => {
          SorUtil.ReplaceAndNotify(ref _client, "Trying to connect", _observers);
          Background.Run(() => OnNextBackground(core, _freshness.Current));
          return Unit.Instance;  // have to return something
        },
        _ => {
          // Error: we are a Core entity but we are getting credentials for CorePlus
          SorUtil.ReplaceAndNotify(ref _client,
            "Enterprise Core+ requires a PQ to be specified", _observers);
          return Unit.Instance;  // have to return something
        });
    }
  }

  private void OnNextBackground(CoreEndpointConfig config, FreshnessToken token) {
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
      if (token.IsCurrentUnsafe) {
        SorUtil.ReplaceAndNotify(ref _client, result, _observers);
      }
    }
  }
}
