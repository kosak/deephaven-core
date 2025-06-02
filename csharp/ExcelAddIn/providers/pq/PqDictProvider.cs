using Deephaven.DheClient.Controller;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PqDictProvider :
  IValueObserverWithCancel<StatusOr<Subscription>>,
  IValueObservable<StatusOr<SharableDict<PersistentQueryInfoMessage>>> {
  private const string UnsetDictText = "No PQ Dict";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<SharableDict<PersistentQueryInfoMessage>>> _observers = new();
  private readonly StatusOrHolder<SharableDict<PersistentQueryInfoMessage>> _dict = new(UnsetDictText);

  public PqDictProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<SharableDict<PersistentQueryInfoMessage>>> observer) {
    lock (_sync) {
      _dict.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamDisposer = _stateManager.SubscribeToSubscription(_endpointId, voc);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    // Do nothing
  }

  private void RemoveObserver(IValueObserver<StatusOr<SharableDict<PersistentQueryInfoMessage>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);

      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
      _dict.Replace(UnsetDictText);
    }
  }

  public void OnNext(StatusOr<Subscription> subscription, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      if (!subscription.GetValueOrStatus(out var sub, out var status)) {
        _dict.ReplaceAndNotify(status, _observers);
        return;
      }

      var progress = StatusOr<SharableDict<PersistentQueryInfoMessage>>.OfTransient(
        "Processing Subscriptions");
      _dict.ReplaceAndNotify(progress, _observers);
      // RefCounted item gets acquired on this thread.
      var sharedDisposer = Repository.Share(sub);
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        // RefCounted item gets released on this thread.
        using var cleanup = sharedDisposer;
        ProcessSubscriptionStream(sub, backgroundToken);
      });
    }
  }

  private void ProcessSubscriptionStream(Subscription sub, CancellationToken token) {
    Int64 version = -1;
    var wantExit = false;
    while (true) {
      StatusOr<SharableDict<PersistentQueryInfoMessage>> newDict;
      if (sub.Next(version) && sub.Current(out version, out var dict)) {
        // TODO(kosak): do something about this sneaky downcast
        newDict = (SharableDict<PersistentQueryInfoMessage>)dict;
      } else {
        newDict = UnsetDictText;
        wantExit = true;
      }

      lock (_sync) {
        if (token.IsCancellationRequested) {
          // Exit this long-running thread.
          return;
        }
        _dict.ReplaceAndNotify(newDict, _observers);
        if (wantExit) {
          // Exit this long-running thread.
          return;
        }
      }
    }
  }
}
