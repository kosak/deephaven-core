using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class CredentialsProviders(WorkerThread workerThread) : IObservable<AddOrRemove<EndpointId>> {
  private readonly Dictionary<EndpointId, CredentialsProvider> _providerMap = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _endpointsObservers = new();

  public IDisposable Subscribe(IObserver<AddOrRemove<EndpointId>> observer) {
    IDisposable? disposable = null;
    workerThread.Invoke(() => {
      _endpointsObservers.Add(observer, out _);
      // To avoid any further possibility of reentrancy while iterating over the dict,
      // make a copy of the keys
      var keys = _providerMap.Keys.ToArray();
      foreach (var endpointId in keys) {
        observer.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
      }
    });

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  public IDisposable Subscribe(EndpointId id, IObserver<StatusOr<CredentialsBase>> observer) {
    IDisposable? disposable = null;
    ApplyTo(id, sp => disposable = sp.Subscribe(observer));

    return ActionAsDisposable.Create(() => {
      workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  public void SetCredentials(EndpointId id, CredentialsBase credentials) {
    ApplyTo(id, cp => cp.SetCredentials(credentials));
  }

  private void ApplyTo(EndpointId id, Action<CredentialsProvider> action) {
    if (workerThread.InvokeIfRequired(() => ApplyTo(id, action))) {
      return;
    }

    if (!_providerMap.TryGetValue(id, out var ep)) {
      ep = new CredentialsProvider(workerThread);
      _providerMap.Add(id, ep);
      _endpointsObservers.OnNext(AddOrRemove<EndpointId>.OfAdd(id));
    }

    action(ep);
  }
}
