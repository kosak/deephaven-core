using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

/// <summary>
/// The job of this class is to observe EndpointConfigs for a given EndpointId,
/// and then provide SessionManagers. If the EndpointConfig does not refer to a Core
/// Plus instance, we notify an error. If it does, we try to connect to that instance in the
/// background. If that connection eventually succeeds, we notify our observers with the
/// corresponding SessionManager object. Otherwise we notify an error.
/// </summary>
internal class SessionManagerProvider :
  IValueObserver<StatusOr<EndpointConfigBase>>,
  IValueObservable<StatusOr<RefCounted<SessionManager>>>,
  IDisposable {
  private const string UnsetSessionManagerText = "[No SessionManager]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly FreshnessSource _freshness;
  private readonly Latch _isSubscribed = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<SessionManager>>> _observers = new();
  private StatusOr<RefCounted<SessionManager>> _session = UnsetSessionManagerText;

  public SessionManagerProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _freshness = new(_sync);
  }

  /// <summary>
  /// Subscribe to session changes
  /// </summary>
  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<SessionManager>>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _session, out _);
      if (_isSubscribed.TrySet()) {
        _upstreamDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _session, "[Disposed]");
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Suppress background messages that might come in after this point.
      _freshness.Refresh();

      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _session, status, _observers);
        return;
      }

      _ = cbase.AcceptVisitor(
        _ => {
          // We are a CorePlus entity but we are getting credentials for core.
          StatusOrUtil.ReplaceAndNotify(ref _session,
            "Persistent Queries are not supported in Community Core", _observers);
          return Unit.Instance;
        },
        corePlus => {
          StatusOrUtil.ReplaceAndNotify(ref _session, "Trying to connect", _observers);
          Background.Run(() => OnNextBackground(corePlus, _freshness.Current));
          return Unit.Instance;
        });
    }
  }

  private void OnNextBackground(CorePlusEndpointConfig config, FreshnessToken token) {
    RefCounted<SessionManager>? smRef = null;
    StatusOr<RefCounted<SessionManager>> result;
    try {
      var sm = EndpointFactory.ConnectToCorePlus(config);
      smRef = RefCounted.Acquire(sm);
      result = smRef;
    } catch (Exception ex) {
      result = ex.Message;
    }
    using var cleanup = smRef;

    lock (_sync) {
      if (!_isDisposed.Value && token.IsCurrentUnsafe) {
        StatusOrUtil.ReplaceAndNotify(ref _session, result, _observers);
      }
    }
  }
}
