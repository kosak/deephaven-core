using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider : IObservable<StatusOr<EndpointConfigBase>> {
  private const string UnsetCredentialsString = "[No Credentials]";
  private readonly object _sync = new();
  private bool _isDisposed = false;
  private readonly ObserverContainer<StatusOr<EndpointConfigBase>> _observers = new();
  private StatusOr<EndpointConfigBase> _credentials = UnsetCredentialsString;

  public IDisposable Subscribe(IObserver<StatusOr<EndpointConfigBase>> observer) {
    throw new Exception("This needs to be an observer of the");
    this_needs_to_be_an_observer_of_the_endpoint_config_dictionary();
    lock (_sync) {
      _observers.AddAndNotify(observer, _credentials, out _);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (isLast) {
        _isDisposed = true;
      }
    }
  }

  public void SetCredentials(EndpointConfigBase newConfig) {
    var newValue = StatusOr<EndpointConfigBase>.OfValue(newConfig);
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      ProviderUtil.SetStateAndNotify(ref _credentials, newValue, _observers);
    }
  }

  public void Resend() {
    lock (_sync) {
      if (_isDisposed) {
        return;
      }
      _observers.OnNext(_credentials);
    }
  }
}
