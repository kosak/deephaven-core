using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
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
  IValueObserverWithCancel<StatusOr<EndpointConfigBase>>,
  IValueObservable<StatusOr<RefCounted<SessionManager>>> {
  private const string UnsetCredentialsText = "[No Credentials]";
  private const string UnsetSessionManagerText = "[No SessionManager]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IDisposable? _upstreamDisposer = null;
  private StatusOr<EndpointConfigBase> _cachedCredentials = UnsetCredentialsText;
  private readonly ObserverContainer<StatusOr<RefCounted<SessionManager>>> _observers = new();
  private StatusOr<RefCounted<SessionManager>> _session = UnsetSessionManagerText;

  public SessionManagerProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to session changes
  /// </summary>
  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<RefCounted<SessionManager>>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _session, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, voc);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    OnNextHelper();
  }

  private void RemoveObserver(IValueObserver<StatusOr<RefCounted<SessionManager>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _cachedCredentials, UnsetCredentialsText);
      StatusOrUtil.Replace(ref _session, UnsetSessionManagerText);
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      StatusOrUtil.Replace(ref _cachedCredentials, credentials);
      OnNextHelper();
    }
  }

  private void OnNextHelper() {
    lock (_sync) {
      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      if (!_cachedCredentials.GetValueOrStatus(out var cbase, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _session, status, _observers);
        return;
      }

      _ = cbase.AcceptVisitor(
        empty => {
          var message = $"Config for {empty.Id} is empty";
          StatusOrUtil.ReplaceAndNotify(ref _session, message, _observers);
          return Unit.Instance;
        },
        core => {
          // We are a CorePlus entity but we are getting credentials for core.
          StatusOrUtil.ReplaceAndNotify(ref _session,
            "Persistent Queries are not supported in Community Core", _observers);
          return Unit.Instance;
        },
        corePlus => {
          var progress = StatusOr<RefCounted<SessionManager>>.OfTransient("Trying to connect");
          StatusOrUtil.ReplaceAndNotify(ref _session, progress, _observers);
          var backgroundToken = _backgroundTokenSource.Token;
          Background.Run(() => OnNextBackground(corePlus, backgroundToken));
          return Unit.Instance;
        });
    }
  }

  private void OnNextBackground(CorePlusEndpointConfig config, CancellationToken token) {
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
      if (token.IsCancellationRequested) {
        return;
      }
      StatusOrUtil.ReplaceAndNotify(ref _session, result, _observers);
    }
  }
}
