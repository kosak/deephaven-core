using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using System.Windows.Forms;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider :
  IObserver<IReadOnlyDictionary<Int64, EndpointConfigBase?>>,
  IObservable<StatusOr<EndpointConfigBase>> {
  private const string UnsetCredentialsString = "[No Credentials]";
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private readonly ObserverContainer<StatusOr<EndpointConfigBase>> _observers = new();
  private StatusOr<EndpointConfigBase> _credentials = UnsetCredentialsString;

  public IDisposable Subscribe(IObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _credentials, out var isFirst);

      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeTo.SubscribeToEndpointDict(this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }
      _isDisposed = true;
      Utility.ClearAndDispose(ref _upstreamDisposer);
      ProviderUtil.SetState(ref _credentials, "[Disposed");
    }
  }

  public void OnNext(IReadOnlyDictionary<long, EndpointConfigBase> dict) {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }

      // Try to find with fast path
      if (!dict.TryGetValue(_keyHint, out var config) || !config.Id.Equals(_endpointId)) {
        // Try to find with differencing and slower path
        var (added, _, _) = _prevDict.CalcDifference(dict);
        var entry = added.FirstOrDefault(kvp => kvp.Value.Endpoint.Equals(_endpointId));
        _keyHint = entry.Key;
        config = entry.Value ?? null;
      }

      _prevDict = dict;

      // need extra logic to debounce duplicates

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
