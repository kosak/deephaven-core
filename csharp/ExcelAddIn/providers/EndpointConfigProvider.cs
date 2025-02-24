using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider : IObservable<StatusOr<EndpointConfigBase>> {
  private const string UnsetCredentialsString = "[No Credentials]";
  private readonly object _sync = new();
  private readonly ObserverContainer<StatusOr<EndpointConfigBase>> _observers = new();
  private StatusOr<EndpointConfigBase> _credentials = UnsetCredentialsString;

  public IDisposable Subscribe(IObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _credentials, out _);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void SetCredentials(EndpointConfigBase newConfig) {
    var newValue = StatusOr<EndpointConfigBase>.OfValue(newConfig);
    lock (_sync) {
      ProviderUtil.SetStateAndNotify(ref _credentials, newValue, _observers);
    }
  }

  public void Resend() {
    lock (_sync) {
      _observers.OnNext(_credentials);
    }
  }
}
