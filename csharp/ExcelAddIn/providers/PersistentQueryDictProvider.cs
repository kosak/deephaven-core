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
  private StatusOr<SharableDict<PersistentQueryInfoMessage>> _dict = UnsetDictText;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();

  private void Func() {
  }



}
