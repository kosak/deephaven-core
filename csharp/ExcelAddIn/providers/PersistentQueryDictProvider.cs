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
  private readonly string _endpointId;
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private IDisposable? _upstreamDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private readonly ObserverContainer<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> _observers = new();
  private StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>> _dict = UnsetDictText;

  public PersistentQueryDictProvider(StateManager stateManager, string endpointId) {
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
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      _isDisposed = true;
      Utility.ClearAndDispose(ref _upstreamDisposer);
      ProviderUtil.SetState(ref _dict, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<Subscription> subscription) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      // Suppress any stale notifications that happen to show up.
      var newCookie = _versionTracker.New();

      if (!subscription.GetValueOrStatus(out var sub, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _dict, status, _observers);
        return;
      }

      ProviderUtil.SetStateAndNotify(ref _dict, "[Processing Subscriptions]", _observers);
      Background666.Run(() => ProcessSubscriptionStream(sub, newCookie));
    }
  }

  private void ProcessSubscriptionStream(Subscription sub, VersionTracker.Cookie cookie) {
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
