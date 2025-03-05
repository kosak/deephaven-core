using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryInfoProvider :
  IObserver<StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>>>,
  IObservable<StatusOr<PersistentQueryInfoMessage>>,
  IDisposable {
  private const string UnsetPqText = "[No Persistent Query]";

  private readonly StateManager _stateManager;
  private readonly string _endpointId;
  private readonly string _pqName;
  private readonly object _sync = new();
  private bool _isDisposed = false;
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
    string endpointId, string pqName) {
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
    lock (_sync) {
      // Do not do this under lock, because we don't want to wait while holding a lock.
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      _isDisposed = true;
      Utility.ClearAndDispose(ref _upstreamDisposer);
      ProviderUtil.SetState(ref _infoMessage, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<IReadOnlyDictionary<Int64, PersistentQueryInfoMessage>> dict) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      if (!dict.GetValueOrStatus(out var d, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _infoMessage, status, _observers);
        return;
      }

      // Try quick lookup using the key hint (and then verify that the hint is correct)
      if (!d.TryGetValue(_keyHint, out var message) || message.Config.Name != _pqName) {
        use_dict_differencing();
        // That didn't work. Try exhaustive slow lookup.
        var stupid = d.FirstOrDefault(v => v.Value.Config.Name == _pqName);
        _keyHint = what;
      }

      if (ReferenceEquals(message, _lastMessage)) {
        // Suppress duplicate messages, which only confuse 
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
