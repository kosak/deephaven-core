using Deephaven.DheClient.Controller;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryDictProvider :
  IObserver<StatusOr<Subscription>>,
  IObservable<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> {
  private const string UnsetDictText = "[No PQ Dict]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private readonly ObserverContainer<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> _observers = new();
  private StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>> _dict = UnsetDictText;

  public PersistentQueryDictProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IObserver<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _dict, out var isFirst);
      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToSubscription(_endpointId);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> observer) {
    // Do not do this under lock, because we don't want to wait while holding a lock.
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }

    // At this point we have no observers.
    IDisposable? disp1;
    lock (_sync) {
      disp1 = Utility.Exchange(ref _upstreamDisposer, null);
    }
    disp1?.Dispose();

    // At this point we are not observing anything.
    // Release our Dictionary asynchronously. (Doesn't really need to be async, but eh)
    lock (_sync) {
      ProviderUtil.SetState(ref _dict, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<Subscription> subscription) {
    lock (_sync) {
      // Regardless of whether the message is a new session or a status message,
      // we make a new cookie so that the processing thread stops feeding stale values.
      // Even before we were called, the caller probably called Dispose on Subscription,
      // so the thread has either exited or is about to.
      var newCookie = _versionTracker.SetNewVersion();
      if (!subscription.GetValueOrStatus(out var sub, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _dict, status, _observers);
        return;
      }

      ProviderUtil.SetStateAndNotify(ref _dict, "[Processing Subscriptions]", _observers);
      Background666.Run(() => ProcessSubscriptionStream(sub, newCookie));
    }
  }

  private void ProcessSubscriptionStream(Subscription sub, VersionTrackerCookie cookie) {
    Int64 version = -1;
    var wantExit = false;
    while (true) {
      StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>> newDict;
      if (sub.Next(version) && sub.Current(out version, out var dict)) {
        newDict = StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>.OfValue(dict);
      } else {
        newDict = "[Subscription closed]";
        wantExit = true;
      }

      lock (_sync) {
        if (!cookie.IsCurrent) {
          return;
        }
        ProviderUtil.SetStateAndNotify(ref _dict, newDict, _observers);
        if (wantExit) {
          return;
        }
      }
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
