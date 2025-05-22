using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Io.Deephaven.Proto.Controller;

namespace Deephaven.ExcelAddIn.Providers;

internal class PqInfoProvider :
  IValueObserverWithCancel<StatusOr<SharableDict<PersistentQueryInfoMessage>>>,
  IValueObservable<StatusOr<PersistentQueryInfoMessage>> {
  private const string UnsetPqText = "[No Persistent Query]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName _pqName;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
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

  public PqInfoProvider(StateManager stateManager,
    EndpointId endpointId, PqName pqName) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _infoMessage, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamDisposer = _stateManager.SubscribeToPersistentQueryDict(
          _endpointId, voc);
      }
      return ActionAsDisposable.Create(() => RemoveObserver(observer));
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<PersistentQueryInfoMessage>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
      StatusOrUtil.Replace(ref _infoMessage, UnsetPqText);
    }
  }

  public void OnNext(StatusOr<SharableDict<PersistentQueryInfoMessage>> dict,
    CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      if (!dict.GetValueOrStatus(out var d, out var status)) {
        _prevDict = SharableDict<PersistentQueryInfoMessage>.Empty;
        // A unique message that will not be equal to anything else and
        // therefore won't mislead the debouncer.
        _lastMessage = new();
        StatusOrUtil.ReplaceAndNotify(ref _infoMessage, status, _observers);
        return;
      }

      // Try to find with fast path
      if (!d.TryGetValue(_keyHint, out var message) || message.Config.Name != _pqName.Name) {
        // That didn't work. Try to find with slower differencing path
        var (added, _, modified) = _prevDict.CalcDifference(d);

        // If there is a new entry, it's in 'added' or 'modified'
        var combined = added.Concat(modified);
        var kvp = combined.FirstOrDefault(kvp => kvp.Value.Config.Name == _pqName.Name);

        // Save the keyhint for next time (it will either be an accurate hint or a zero).
        // Incorrect hints are not a problem.
        _keyHint = kvp.Key;
        message = kvp.Value;
      }

      if (ReferenceEquals(message, _lastMessage)) {
        // Debounce duplicate messages
        return;
      }
      _lastMessage = message;

      if (message == null) {
        StatusOrUtil.ReplaceAndNotify(ref _infoMessage, $"PQ \"{_pqName}\" not found", _observers);
        return;
      }

      StatusOrUtil.ReplaceAndNotify(ref _infoMessage, message, _observers);
    }
  }
}
