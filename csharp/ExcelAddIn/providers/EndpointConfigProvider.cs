using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider :
  IValueObserver<SharableDict<EndpointConfigBase>>,
  IValueObservable<StatusOr<EndpointConfigBase>>,
  IDisposable {
  private const string UnsetCredentialsString = "[No Config]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly Latch _needsSubscription = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamSubscription = null;
  private Int64 _keyHint = -1;
  private SharableDict<EndpointConfigBase> _prevDict = SharableDict<EndpointConfigBase>.Empty;
  private EndpointConfigBase? _prevConfig = null;
  private readonly ObserverContainer<StatusOr<EndpointConfigBase>> _observers = new();
  private StatusOr<EndpointConfigBase> _credentials = UnsetCredentialsString;

  public EndpointConfigProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IValueObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _credentials, out _);

      if (_needsSubscription.TrySet()) {
        _upstreamSubscription = _stateManager.SubscribeToEndpointDict(this);
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
      Utility.ClearAndDispose(ref _upstreamSubscription);
      StatusOrUtil.Replace(ref _credentials, "[Disposed]");
    }
  }

  public void OnNext(SharableDict<EndpointConfigBase> dict) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Try to find with fast path
      if (!dict.TryGetValue(_keyHint, out var config) || !config.Id.Equals(_endpointId)) {
        // That didn't work. Try to find with slower differencing path
        var (added, _, modified) = _prevDict.CalcDifference(dict);

        // If there is a new entry, it's in 'added' or 'modified'
        var combined = added.Concat(modified);
        var kvp = combined.FirstOrDefault(kvp => kvp.Value.Id.Equals(_endpointId));

        // Save the keyhint for next time (it will either be an accurate hint or a zero)
        _keyHint = kvp.Key;
        config = kvp.Value;
      }

      _prevDict = dict;
      if (ReferenceEquals(_prevConfig, config)) {
        // Debounce duplicate messages
        return;
      }
      _prevConfig = config;
      
      if (config == null) {
        StatusOrUtil.ReplaceAndNotify(ref _credentials, UnsetCredentialsString, _observers);
        return;
      }

      var value = StatusOr<EndpointConfigBase>.OfValue(config);
      StatusOrUtil.ReplaceAndNotify(ref _credentials, value, _observers);
    }
  }

  public void Resend() {
    lock (_sync) {
      _observers.OnNext(_credentials);
    }
  }
}
