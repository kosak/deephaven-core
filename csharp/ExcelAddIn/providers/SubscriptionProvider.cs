using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SubscriptionProvider :
  IObserver<StatusOr<SessionManager>>,
  IObservable<StatusOr<Subscription>> {
  private const string UnsetSubText = "[No Subscription Dict]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
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
    // Do not do this under lock, because we don't want to wait while holding a lock.
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }

    // At this point we have no observers.
    IDisposable? disp;
    lock (_sync) {
      disp = Utility.Exchange(ref _upstreamDisposer, null);
    }
    disp?.Dispose();

    // At this point we are not observing anything.
    // Release our Subscription asynchronously.
    lock (_sync) {
      ProviderUtil.SetState(ref _subscription, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<SessionManager> sessionManager) {
    lock (_sync) {
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
}
