using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryDictProvider :
  IObserver<StatusOr<SessionManager>>,
  IObservable<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> {
  private const string UnsetDictText = "[No PQ Dict]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<Subscription> _subscription = UnsetDictText;
  private StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>> _dict = UnsetDictText;
  private readonly ObserverContainer<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>> _observers = new();

  public PersistentQueryDictProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IObserver<StatusOr<SharableDict<PersistentQueryInfoMessage>>> observer) {
    lock (_sync) {
      _observers.Add(observer, out var isFirst);
      _observers.OnNextOne(observer, _dict);
      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToSession(_endpointId);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<SharableDict<PersistentQueryInfoMessage>>> observer) {
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
      Background666.InvokeDispose(Utility.Exchange(ref _subscription, UnsetDictText));
    }
  }

  public void OnNext(StatusOr<SessionManager> sessionManager) {
    lock (_sync) {
      // Regardless of whether the message is a new session or a status message,
      // we make a new cookie so that the processing thread stops.
      var newCookie = _versionTracker.SetNewVersion();
      if (!sessionManager.GetValueOrStatus(out var sm, out var status)) {
        // probably have to call Subscription.Cancel() to get it to stop
        _ = _versionTracker.SetNewVersion();
        SetStateLocked(ref _subscription, status);
        _observers.SetStateAndNotify(ref _dict, status);
        return;
      }

      var sub = sm.Subscribe();
      // Our value is "subscription" with a dependency on SessionManager
      using var newState = StatusOr<Subscription>.OfValue(sub, sessionManager);
      SetStateLocked(ref _subscription, newState);
      SetStateLocked(ref _dict, StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>.OfValue(
        ImmutableDictionary<Int64, PersistentQueryInfoMessage>.Empty));
      // probably have to call Subscription.Cancel() to get it to stop
      var sessionCopy = newState.Share();
      Background666.Run(() => ProcessSubscriptionStream(sessionCopy, newCookie));
    }
  }

  private void SetStateLocked<T>(ref StatusOr<T> existing, StatusOr<T> newItem) {
    Background666.InvokeDispose(existing);
    existing = newItem.Share();
  }

  private void ProcessSubscriptionStream(StatusOr<Subscription> subCopy,
    VersionTrackerCookie cookie) {
    using var cleanup1 = subCopy;
    var (sub, _) = subCopy;
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
