using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class DefaultEndpointProvider : IValueObservable<StatusOr<EndpointId>> {
  private const string UnsetEndpointText = "[No endpoint]";
  private readonly object _sync = new();
  private readonly ObserverContainer<StatusOr<EndpointId>> _observers = new();
  private StatusOr<EndpointId> _endpointId = UnsetEndpointText;
  
  public IDisposable Subscribe(IValueObserver<StatusOr<EndpointId>> observer) {
    lock (_sync) {
      StatusOrUtil.AddObserverAndNotify(_observers, observer, _endpointId, out _);
    }

    return ActionAsDisposable.Create(() => {
      lock (_sync) {
        _observers.Remove(observer, out _);
      }
    });
  }

  public EndpointId? Value {
    get {
      lock (_sync) {
        return _endpointId.GetValueOrStatus(out var value, out _) ? value : null;
      }
    }
  }

  public void SetValue(EndpointId? endpointId) {
    lock (_sync) {
      if (endpointId == null) {
        StatusOrUtil.ReplaceAndNotify(ref _endpointId, UnsetEndpointText, _observers);
      } else {
        StatusOrUtil.ReplaceAndNotify(ref _endpointId, endpointId, _observers);
      }
    }
  }
}
