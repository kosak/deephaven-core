using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SubscriptionProvider :
  IValueObserver<StatusOr<RefCounted<SessionManager>>>,
  IValueObservable<StatusOr<RefCounted<Subscription>>>,
  IDisposable {
  private const string UnsetSubText = "[No Subscription]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly Latch _isSubscribed = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<RefCounted<Subscription>>> _observers = new();
  private StatusOr<RefCounted<Subscription>> _subscription = UnsetSubText;

  public SubscriptionProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<RefCounted<Subscription>>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _subscription, out _);
      if (_isSubscribed.TrySet()) {
        _upstreamDisposer = _stateManager.SubscribeToSessionManager(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IValueObserver<StatusOr<RefCounted<Subscription>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _upstreamDisposer);
      SorUtil.Replace(ref _subscription, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<RefCounted<SessionManager>> sessionManager) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      if (!sessionManager.GetValueOrStatus(out var smRef, out var status)) {
        SorUtil.ReplaceAndNotify(ref _subscription, status, _observers);
        return;
      }

      // Subscribe is a cheap (and nonblocking) operation. If it were not cheap
      // or if it were blocking, we would have to do it on a background thread.
      var sub = smRef.Value.Subscribe();
      // newState holds a Subscription with a dependency on a SessionManager
      using var subRef = RefCounted.Acquire(sub, smRef);
      SorUtil.ReplaceAndNotify(ref _subscription, subRef, _observers);
    }
  }
}
