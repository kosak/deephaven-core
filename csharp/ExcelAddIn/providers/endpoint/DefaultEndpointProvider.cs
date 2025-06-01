using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class DefaultEndpointProvider : IValueObservable<StatusOr<EndpointId>> {
  private const string UnsetEndpointText = "[No endpoint]";
  private readonly object _sync = new();
  private readonly ObserverContainer<StatusOr<EndpointId>> _observers = new();
  private readonly StatusOrHolder<EndpointId> _endpointId = new(UnsetEndpointText);
  
  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<EndpointId>> observer) {
    lock (_sync) {
      _endpointId.AddObserverAndNotify(_observers, observer, out _);
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    // Do nothing.
  }

  private void RemoveObserver(IValueObserver<StatusOr<EndpointId>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
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
        _endpointId.ReplaceAndNotify(UnsetEndpointText, _observers);
      } else {
        _endpointId.ReplaceAndNotify(endpointId, _observers);
      }
    }
  }
}
