using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class EndpointConfigProvider : IObservable<StatusOr<EndpointConfigBase>> {
  private const string UnsetCredentialsString = "[No Credentials]";
  private readonly SequentialExecutor _executor = new();
  private readonly object _sync = new();
  private readonly ObserverContainer<StatusOr<EndpointConfigBase>> _observers;
  private StatusOr<EndpointConfigBase> _credentials;

  public EndpointConfigProvider() {
    _observers = new(_executor);
    _credentials = MakeState(UnsetCredentialsString);
  }

  public IDisposable Subscribe(IObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.Add(observer, out _);
      _observers.OnNextOne(observer, _credentials);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<EndpointConfigBase>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void SetCredentials(EndpointConfigBase newConfig) {
    lock (_sync) {
      _credentials = StatusOr<EndpointConfigBase>.OfValue(newConfig);
      _observers.OnNext(_credentials);
    }
  }

  public void Resend() {
    lock (_sync) {
      _observers.OnNext(_credentials);
    }
  }

  private static StatusOr<EndpointConfigBase> MakeState(string status) {
    return StatusOr<EndpointConfigBase>.OfStatus(status);
  }
}
