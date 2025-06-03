using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Refcounting;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class PqSubscriptionProvider :
  IValueObserverWithCancel<StatusOr<SessionManager>>,
  IValueObservable<StatusOr<Subscription>> {
  private const string UnsetSubText = "No Subscription";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<Subscription>> _observers = new();
  private readonly StatusOrHolder<Subscription> _subscription = new(UnsetSubText);

  public PqSubscriptionProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<Subscription>> observer) {
    lock (_sync) {
      _subscription.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamDisposer = _stateManager.SubscribeToSessionManager(_endpointId, voc);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    // Do nothing
  }

  private void RemoveObserver(IValueObserver<StatusOr<Subscription>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
      _subscription.Replace(UnsetSubText);
    }
  }

  public void OnNext(StatusOr<SessionManager> sessionManager,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      if (!sessionManager.GetValueOrStatus(out var sm, out var status)) {
        _subscription.ReplaceAndNotify(status, _observers);
        return;
      }

      // Subscribe is a cheap (and nonblocking) operation. If it were not cheap
      // or if it were blocking, we would have to do it on a background thread.
      var sub = sm.Subscribe();
      // The subscription is stored as a ref with a dependency on a SessionManager
      using var subDisposer = Repository.Register(sub, sm);
      _subscription.ReplaceAndNotify(sub, _observers);
    }
  }
}
