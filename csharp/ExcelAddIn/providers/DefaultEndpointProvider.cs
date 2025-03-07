using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class DefaultEndpointProvider : IStatusObservable<EndpointId> {
  private const string UnsetEndpointText = "[No endpoint]";
  private readonly object _sync = new();
  private readonly ObserverContainer<EndpointId> _observers = new();
  private StatusOr<EndpointId> _endpointId = UnsetEndpointText;
  
  public IDisposable Subscribe(IStatusObserver<EndpointId> observer) {
    lock (_sync) {
      SorUtil.AddObserverAndNotify(_observers, observer, _endpointId, out _);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IStatusObserver<EndpointId> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Set(EndpointId? endpointId) {
    lock (_sync) {
      if (endpointId == null) {
        SorUtil.ReplaceAndNotify(ref _endpointId, UnsetEndpointText, _observers);
      } else {
        SorUtil.ReplaceAndNotify(ref _endpointId, endpointId, _observers);
      }
    }
  }
}
