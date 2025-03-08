using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PersistentQueryInfoProvider :
  IValueObserver<StatusOr<SharableDict<PersistentQueryInfoMessage>>>,
  IValueObservable<StatusOr<PersistentQueryInfoMessage>>,
  IDisposable {
  private const string UnsetPqText = "[No Persistent Query]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName _pqName;
  private readonly object _sync = new();
  private readonly Latch _isSubscribed = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<PersistentQueryInfoMessage>> _observers = new();
  private StatusOr<PersistentQueryInfoMessage> _infoMessage = UnsetPqText;
  /// <summary>
  /// Cached value of the last named lookup. Is only a hint so it's ok if it is some garbage value.
  /// </summary>
  private Int64 _keyHint = -1;
  private SharableDict<PersistentQueryInfoMessage> _prevDict = SharableDict<PersistentQueryInfoMessage>.Empty;

  /// <summary>
  /// Cached value of the last message. Used to deduplicate notifications.
  /// </summary>
  private PersistentQueryInfoMessage? _lastMessage = new();

  public PersistentQueryInfoProvider(StateManager stateManager,
    EndpointId endpointId, PqName pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _infoMessage, out _);
      if (_isSubscribed.TrySet()) {
        _upstreamDisposer = _stateManager.SubscribeToPersistentQueryDict(
          _endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _upstreamDisposer);
      SorUtil.Replace(ref _infoMessage, "[Disposing]");
    }
  }

  public void OnNext(StatusOr<SharableDict<PersistentQueryInfoMessage>> dict) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      if (!dict.GetValueOrStatus(out var d, out var status)) {
        _prevDict = SharableDict<PersistentQueryInfoMessage>.Empty;
        _lastMessage = null;
        SorUtil.ReplaceAndNotify(ref _infoMessage, status, _observers);
        return;
      }

      // Try to find with fast path
      if (!d.TryGetValue(_keyHint, out var message) || message.Config.Name != _pqName.Name) {
        // That didn't work. Try to find with slower differencing path
        var (added, _, modified) = _prevDict.CalcDifference(d);

        // If there is a new entry, it's in 'added' or 'modified'
        var combined = added.Concat(modified);
        var kvp = combined.FirstOrDefault(kvp => kvp.Value.Config.Name == _pqName.Name);

        // Save the keyhint for next time (it will either be an accurate hint or a zero)
        _keyHint = kvp.Key;
        message = kvp.Value;
      }

      if (ReferenceEquals(message, _lastMessage)) {
        // Debounce duplicate messages
        return;
      }
      _lastMessage = message;

      if (message == null) {
        SorUtil.ReplaceAndNotify(ref _infoMessage, $"PQ \"{_pqName}\" not found", _observers);
        return;
      }

      SorUtil.ReplaceAndNotify(ref _infoMessage, message, _observers);
    }
  }
}
