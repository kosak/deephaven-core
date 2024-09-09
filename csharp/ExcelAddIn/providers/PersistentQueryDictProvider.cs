﻿using Deephaven.DheClient.Controller;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryDictProvider :
  IValueObserver<StatusOr<RefCounted<Subscription>>>,
  IValueObservable<StatusOr<SharableDict<PersistentQueryInfoMessage>>>,
  IDisposable {
  private const string UnsetDictText = "[No PQ Dict]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly FreshnessTokenSource _freshness;
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
        _upstreamDisposer = _stateManager.SubscribeToSubscription(_endpointId, this);
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
      StatusOrUtil.Replace(ref _dict, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<RefCounted<Subscription>> subscription) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }
      // Suppress any stale notifications that happen to show up.
      var token = _freshness.Refresh();

      if (!subscription.GetValueOrStatus(out var sub, out var status)) {
        StatusOrUtil.ReplaceAndNotify(ref _dict, status, _observers);
        return;
      }

      StatusOrUtil.ReplaceAndNotify(ref _dict, "[Processing Subscriptions]", _observers);
      var subShare = sub.Share();
      Background.Run(() => {
        using var cleanup = subShare;
        ProcessSubscriptionStream(subShare, token);
      });
    }
  }

  private void ProcessSubscriptionStream(RefCounted<Subscription> subRef, FreshnessToken token) {
    Int64 version = -1;
    var wantExit = false;
    var sub = subRef.Value;
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
        if (!token.IsCurrent) {
          return;
        }
        StatusOrUtil.ReplaceAndNotify(ref _dict, newDict, _observers);
        if (wantExit) {
          return;
        }
      }
    }
  }
}
