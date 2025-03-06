using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider :
  IObserver<SharableDict<EndpointConfigBase>>,
  IObservable<StatusOr<EndpointConfigBase>>,
  IDisposable {
  private const string UnsetCredentialsString = "[No Credentials]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly Latch _needsSubscription = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamSubscription = null;
  private long _keyHint = 0;
  private SharableDict<EndpointConfigBase> _prevDict = SharableDict<EndpointConfigBase>.Empty;
  private EndpointConfigBase? _prevConfig = null;
  private readonly ObserverContainer<StatusOr<EndpointConfigBase>> _observers = new();
  private StatusOr<EndpointConfigBase> _credentials = UnsetCredentialsString;

  public EndpointConfigProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _credentials, out _);

      if (_needsSubscription.TrySet()) {
        _upstreamSubscription = _stateManager.SubscribeToEndpointDict(this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<EndpointConfigBase>> observer) {
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
      ProviderUtil.SetState(ref _credentials, "[Disposed");
    }
  }

  public void OnNext(SharableDict<EndpointConfigBase> dict) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Try to find with fast path
      if (!dict.TryGetValue(_keyHint, out var config) || !config.Id.Equals(_endpointId)) {
        // Try to find with differencing and slower path
        var (added, _, modified) = _prevDict.CalcDifference(dict);
        var combined = added.Concat(modified);
        var entry = combined.FirstOrDefault(kvp => kvp.Value.Id.Equals(_endpointId));
        _keyHint = entry.Key;
        config = entry.Value ?? null;
      }

      _prevDict = dict;
      if (ReferenceEquals(_prevConfig, config)) {
        return;
      }
      _prevConfig = config;
      
      if (config == null) {
        ProviderUtil.SetStateAndNotify(ref _credentials, UnsetCredentialsString, _observers);
        return;
      }

      var value = StatusOr<EndpointConfigBase>.OfValue(config);
      ProviderUtil.SetStateAndNotify(ref _credentials, value, _observers);
    }
  }
}
