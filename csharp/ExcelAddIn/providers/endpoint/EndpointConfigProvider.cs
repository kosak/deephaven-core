using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider :
  IValueObserverWithCancel<SharableDict<EndpointConfigBase>>,
  IValueObservable<StatusOr<EndpointConfigBase>> {
  private const string UnsetCredentialsString = "[No Config]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private IDisposable? _upstreamSubscription = null;
  private Int64 _keyHint = -1;
  private SharableDict<EndpointConfigBase> _prevDict = SharableDict<EndpointConfigBase>.Empty;
  private EndpointConfigBase? _prevConfig = null;
  private readonly ObserverContainer<StatusOr<EndpointConfigBase>> _observers = new();
  private readonly StatusOrHolder<EndpointConfigBase> _credentials = new(UnsetCredentialsString);

  public EndpointConfigProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _credentials.AddObserverAndNotify(_observers, observer, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamSubscription = _stateManager.SubscribeToEndpointDict(voc);
      }
    }
    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    // Nothing to do for Retry.
  }

  private void RemoveObserver(IValueObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamSubscription);
      _credentials.Replace(UnsetCredentialsString);
    }
  }

  public void OnNext(SharableDict<EndpointConfigBase> dict, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      // Try to find with fast path
      if (!dict.TryGetValue(_keyHint, out var config) || !config.Id.Equals(_endpointId)) {
        // That didn't work. Try to find with slower differencing path
        var (added, _, modified) = _prevDict.CalcDifference(dict);

        // If there is a new entry, it's in 'added' or 'modified'
        var combined = added.Concat(modified);
        var kvp = combined.FirstOrDefault(kvp => kvp.Value.Id.Equals(_endpointId));

        // Save the keyhint for next time (it will either be an accurate hint or a zero,
        // and it's ok if the hint is wrong).
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
        _credentials.ReplaceAndNotify(UnsetCredentialsString, _observers);
        return;
      }

      var value = StatusOr<EndpointConfigBase>.OfValue(config);
      _credentials.ReplaceAndNotify(value, _observers);
    }
  }

  public void Resend() {
    lock (_sync) {
      _credentials.Notify(_observers);
    }
  }
}
