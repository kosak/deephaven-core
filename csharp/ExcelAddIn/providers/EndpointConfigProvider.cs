using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider :
  IStatusObserver<SharableDict<EndpointConfigEntry>>,
  IStatusObservable<EndpointConfigBase>,
  IDisposable {
  private const string UnsetCredentialsString = "[No Credentials]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private readonly Latch _needsSubscription = new();
  private readonly Latch _isDisposed = new();
  private IDisposable? _upstreamSubscription = null;
  private long _keyHint = 0;
  private SharableDict<EndpointConfigEntry> _prevDict = SharableDict<EndpointConfigEntry>.Empty;
  private EndpointConfigBase? _prevConfig = null;
  private readonly ObserverContainer<EndpointConfigBase> _observers = new();
  private StatusOr<EndpointConfigBase> _credentials = UnsetCredentialsString;

  public EndpointConfigProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IDisposable Subscribe(IStatusObserver<EndpointConfigBase> observer) {
    lock (_sync) {
      SorUtil.AddObserverAndNotify(_observers, observer, _credentials, out _);

      if (_needsSubscription.TrySet()) {
        _upstreamSubscription = _stateManager.SubscribeToEndpointDict(this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IStatusObserver<EndpointConfigBase> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Dispose() {
    lock (_sync) {
      if (!_isDisposed.TrySet()) {
        return;
      }
      Utility.ClearAndDispose(ref _upstreamSubscription);
      SorUtil.Replace(ref _credentials, "[Disposed]");
    }
  }

  public void OnStatus(string status) {
    if (_isDisposed.Value) {
      return;
    }
    _prevDict = SharableDict<EndpointConfigEntry>.Empty;
    _prevConfig = null;
    SorUtil.ReplaceAndNotify(ref _credentials, status, _observers);
  }

  public void OnNext(SharableDict<EndpointConfigEntry> dict) {
    lock (_sync) {
      if (_isDisposed.Value) {
        return;
      }

      // Try to find with fast path
      if (!dict.TryGetValue(_keyHint, out var configEntry) || !configEntry.Id.Equals(_endpointId)) {
        // Try to find with slower differencing path
        var (added, _, modified) = _prevDict.CalcDifference(dict);
        var combined = added.Concat(modified);
        var kvp = combined.FirstOrDefault(kvp => kvp.Value.Id.Equals(_endpointId));
        _keyHint = kvp.Key;
        configEntry = kvp.Value;
      }
      var config = configEntry?.Config;
      _prevDict = dict;
      if (ReferenceEquals(_prevConfig, config)) {
        return;
      }
      _prevConfig = config;
      
      if (config == null) {
        SorUtil.ReplaceAndNotify(ref _credentials, UnsetCredentialsString, _observers);
        return;
      }

      var value = StatusOr<EndpointConfigBase>.OfValue(config);
      SorUtil.ReplaceAndNotify(ref _credentials, value, _observers);
    }
  }
}
