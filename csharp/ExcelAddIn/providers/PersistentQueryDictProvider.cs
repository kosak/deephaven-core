using Deephaven.DheClient.Controller;
using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryDictProvider :
  IObserver<StatusOr<SessionManager>>,
  IObservable<StatusOr<SharableDict<PersistentQueryInfoMessage>>> {
  private const string UnsetDictText = "[No PQ Dict]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private IDisposable? _upstreamSubscriptionDisposer = null;
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<SessionManager> _sessionManager = UnsetDictText;
  private StatusOr<SharableDict<PersistentQueryInfoMessage>> _dict = UnsetDictText;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();

  public void OnNext(StatusOr<SessionManager> sessionManager) {
    lock (_sync) {
      if (!sessionManager.GetValueOrStatus(out var sm, out var status)) {
        CancelNubbinLocked();
        _observers.SetStateAndNotify(ref _dict, status);
        return;
      }

      _subscription = sm.Subscribe();
      ZamboniThread.Start();
    }
  }

  private void ZamboniThread(Subscription sub, VersionTrackerCookie cookie) {
    Int64 version = -1;
    while (true) {
      if (!sub.Next(version) || !sub.Current(out version, out var dict)) {
        // zamboni share also involves checking the version cookie
        ZamboniShare(cancelled);
        return;
      }

      lock (_sync) {
        // zamboni share also involves checking the version cookie
        ZamboniShare(dict);
      }
    }
  }
}
