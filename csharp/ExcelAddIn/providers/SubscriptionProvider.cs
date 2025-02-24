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
  private StatusOr<Subscription> _subscription = UnsetSubText;
  private readonly ObserverContainer<StatusOr<Subscription>> _observers = new();

  public SubscriptionProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IObserver<StatusOr<Subscription>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _subscription);
      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToSession(_endpointId);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<Subscription>> observer) {
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }

    IDisposable? disp1;
    lock (_sync) {
      disp1 = Utility.Exchange(ref _upstreamDisposer, null);
    }
    disp1?.Dispose();

    lock (_sync) {
      _observers.SetState(ref _subscription, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<SessionManager> sessionManager) {
    lock (_sync) {
      if (!sessionManager.GetValueOrStatus(out var sm, out var status)) {
        _observers.SetStateAndNotify(ref _subscription, status);
        return;
      }

      var sub = sm.Subscribe();
      // NewState is Subscription with a dependency on sessionManager
      using var newState = StatusOr<Subscription>.OfValue(sub, sessionManager);
      _observers.SetStateAndNotify(ref _subscription, newState);
    }
  }
}
