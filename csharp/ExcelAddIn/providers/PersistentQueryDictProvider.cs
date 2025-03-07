using Deephaven.DheClient.Controller;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryDictProvider :
  IValueObserver<StatusOr<Subscription>>,
  IValueObservable<StatusOr<SharableDict<PersistentQueryInfoMessage>>>,
  IDisposable {
  private const string UnsetDictText = "[No PQ Dict]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly FreshnessSource _freshness;
  private readonly Latch _subscribeDone = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<SharableDict<PersistentQueryInfoMessage>>> _observers = new();
  private StatusOr<SharableDict<PersistentQueryInfoMessage>> _dict = UnsetDictText;

  public PersistentQueryDictProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _freshness = new(_sync);
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<SharableDict<PersistentQueryInfoMessage>>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _dict, out _);
      if (_subscribeDone.TrySet()) {
        _upstreamDisposer = _stateManager.SubscribeToSubscription(_endpointId);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IValueObserver<StatusOr<SharableDict<PersistentQueryInfoMessage>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _upstreamDisposer);
      SorUtil.Replace(ref _dict, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<Subscription> subscription) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      // Suppress any stale notifications that happen to show up.
      _freshness.Refresh();

      if (!subscription.GetValueOrStatus(out var sub, out var status)) {
        SorUtil.ReplaceAndNotify(ref _dict, status, _observers);
        return;
      }

      SorUtil.ReplaceAndNotify(ref _dict, "[Processing Subscriptions]", _observers);
      Background.Run(() => ProcessSubscriptionStream(sub, _freshness.Current));
    }
  }

  private void ProcessSubscriptionStream(Subscription sub, FreshnessToken token) {
    Int64 version = -1;
    var wantExit = false;
    while (true) {
      StatusOr<SharableDict<PersistentQueryInfoMessage>> newDict;
      if (sub.Next(version) && sub.Current(out version, out var dict)) {
        // TODO(kosak): do something about this sneaky downcast
        newDict = (SharableDict<PersistentQueryInfoMessage>)dict;
      } else {
        newDict = "[Subscription closed]";
        wantExit = true;
      }

      lock (_sync) {
        if (!token.IsCurrentUnsafe) {
          return;
        }
        SorUtil.ReplaceAndNotify(ref _dict, newDict, _observers);
        if (wantExit) {
          return;
        }
      }
    }
  }
}
