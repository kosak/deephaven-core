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
  private StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>> _dict = UnsetDictText;
  private readonly ObserverContainer<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> _observers = new();

  public PersistentQueryDictProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IObserver<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _dict);
      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToSubscription(_endpointId);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> observer) {
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
      _observers.SetState(ref _dict, UnsetDictText);
    }
  }

  public void OnNext(StatusOr<Subscription> subscription) {
    lock (_sync) {
      // Regardless of whether the message is a new session or a status message,
      // we make a new cookie so that the processing thread stops.
      var newCookie = _versionTracker.SetNewVersion();
      if (!subscription.GetValueOrStatus(out var sub, out var status)) {
        _observers.SetStateAndNotify(ref _dict, status);
        return;
      }

      _observers.SetStateAndNotify(ref _dict, "[Processing Subscriptions]");
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
        _observers.SetStateAndNotify(ref _dict, newDict);
        if (wantExit) {
          return;
        }
      }
    }
  }
}
