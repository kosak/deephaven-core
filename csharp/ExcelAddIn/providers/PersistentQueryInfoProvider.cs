using Deephaven.DheClient.Session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryInfoProvider :
  IObserver<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>>,
  IObservable<StatusOr<PersistentQueryInfoMessage>> {
  private const string UnsetPqText = "[No Persistent Query]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly string _pqName;
  private readonly object _sync = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<PersistentQueryInfoMessage>> _observers = new();
  private StatusOr<PersistentQueryInfoMessage> _infoMessage = UnsetPqText;
  /// <summary>
  /// Cached value of the last named lookup. Is only a hint so it's ok if it is some garbage value.
  /// </summary>
  private Int64 _keyHint = -1;

  /// <summary>
  /// Cached value of the last message. Used to deduplicate notifications.
  /// </summary>
  private PersistentQueryInfoMessage? _lastMessage = new();

  public PersistentQueryInfoProvider(StateManager stateManager,
    EndpointId endpointId, string pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
  }

  public IDisposable Subscribe(IObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _infoMessage, out var isFirst);
      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToPersistentQueryDict(
          _endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
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
    // Release our message asynchronously. (Doesn't really need to be async, but eh)
    lock (_sync) {
      ProviderUtil.SetState(ref _infoMessage, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>> dict) {
    lock (_sync) {
      if (!dict.GetValueOrStatus(out var d, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _infoMessage, status, _observers);
        return;
      }

      // Try quick lookup
      if (!d.TryGetValue(_keyHint, out var message) || message.Config.Name != _pqName) {
        // Quick lookup didn't work. Try slow lookup.
        message = d.Values.FirstOrDefault(v => v.Config.Name == _pqName);
      }

      if (ReferenceEquals(message, _lastMessage)) {
        // If the last message is the same as this one (i.e. both the same or both
        // null), then we don't have to resend a notification, because it would just
        // be noisy for the observer.
        return;
      }

      _lastMessage = message;

      StatusOr<PersistentQueryInfoMessage> result;
      if (message == null) {
        result = $"PQ \"{_pqName}\" not found";
      } else {
        _keyHint = message.Config.Serial;
        result = StatusOr<PersistentQueryInfoMessage>.OfValue(message);
      }

      // Doesn't really matter because PersistentQueryInfoMessage isn't disposable anyway,
      // but doesn't hurt.
      using var cleanup = result;

      ProviderUtil.SetStateAndNotify(ref _infoMessage, result, _observers);
    }
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }
}
