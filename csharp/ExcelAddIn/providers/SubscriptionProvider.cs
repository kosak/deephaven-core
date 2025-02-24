using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SubscriptionProvider :
  IObserver<StatusOr<SessionManager>>,
  IObservable<StatusOr<Subscription>> {
  private const string UnsetSubText = "[No Subscription]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<Subscription>> _observers = new();
  private StatusOr<Subscription> _subscription = UnsetSubText;

  public SubscriptionProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IObserver<StatusOr<Subscription>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _subscription, out var isFirst);
      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToSession(_endpointId);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<Subscription>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      _isDisposed = true;
      Utility.ClearAndDispose(ref _upstreamDisposer);
      ProviderUtil.SetState(ref _subscription, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<SessionManager> sessionManager) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      if (!sessionManager.GetValueOrStatus(out var sm, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _subscription, status, _observers);
        return;
      }

      // Subscribe is a cheap (and nonblocking) operation. If it were not cheap
      // or if it were blocking, we would have to do it on a background thread.
      var sub = sm.Subscribe();
      // newState holds a Subscription with a dependency on a SessionManager
      using var newState = StatusOr<Subscription>.OfValue(sub, sessionManager);
      ProviderUtil.SetStateAndNotify(ref _subscription, newState, _observers);
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
