using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class DefaultEndpointProvider : IObservable<EndpointId?> {
  private readonly object _sync = new();
  private readonly ObserverContainer<EndpointId?> _observers = new();
  private EndpointId? _endpointId = null;
  
  public IDisposable Subscribe(IObserver<EndpointId?> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _endpointId, out _);
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<EndpointId?> observer) {
    lock (_sync) {
      _observers.Remove(observer, out _);
    }
  }

  public void Set(EndpointId? endpointId) {
    lock (_sync) {
      _endpointId = endpointId;
      _observers.OnNext(endpointId);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}
