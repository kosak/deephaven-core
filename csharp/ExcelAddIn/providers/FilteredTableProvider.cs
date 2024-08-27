using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using static System.Collections.Specialized.BitVector32;

namespace Deephaven.ExcelAddIn.Providers;

public class StateManager {
  public readonly WorkerThread WorkerThread = WorkerThread.Create();
  private readonly EndpointStateProviders _endpointStateProviders;

  public StateManager() {
    _endpointStateProviders = new EndpointStateProviders(WorkerThread);
  }

  public IDisposable SubscribeToEndpoints(IObserver<AddOrRemove<EndpointId>> observer) {
    return _endpointStateProviders.Subscribe(observer);
  }

  public IDisposable SubscribeToEndpoint(EndpointId endpointId, IObserver<EndpointState> observer) {
    return _endpointStateProviders.Subscribe(endpointId, observer);
  }

  public IDisposable SubscribeToTableTriple(TableTriple descriptor,
    string filter,
    IObserver<StatusOr<TableHandle>> observer) {
    var mco = new MyComboObserver(WorkerThread, descriptor, filter, observer);
    return _endpointStateProviders.Subscribe(descriptor.EndpointId, mco);
  }

  public void SetCredentials(EndpointId id, CredentialsBase credentials) {
    _endpointStateProviders.SetCredentials(id, credentials);
  }

  public void Reconnect(EndpointId id) {
    _endpointStateProviders.Reconnect(id);
  }
}

public record AddOrRemove<T>(bool IsAdd, T Value) {
  public static AddOrRemove<T> OfAdd(T value) {
    return new AddOrRemove<T>(true, value);
  }
}

public record EndpointId(string Id) {
  public string HumanReadableString => Id == "" ? "[Default]" : Id;

  public override string ToString() => HumanReadableString;
}

public record PersistentQueryId(string Id);


internal class EndpointStateProviders : IObservable<AddOrRemove<EndpointId>> {
  private readonly WorkerThread _workerThread;

  private readonly Dictionary<EndpointId, EndpointStateProvider> _providerMap = new();
  private readonly ObserverContainer<AddOrRemove<EndpointId>> _endpointsObservers = new();

  public EndpointStateProviders(WorkerThread workerThread) => _workerThread = workerThread;

  public void SetCredentials(EndpointId id, CredentialsBase credentials) {
    ApplyTo(id, ep => ep.SetCredentials(credentials));
  }

  public void Reconnect(EndpointId id) {
    ApplyTo(id, ep => ep.Reconnect());
  }

  public IDisposable Subscribe(IObserver<AddOrRemove<EndpointId>> observer) {
    IDisposable? disposable = null;
    // We need to run this on our worker thread because we want to protect
    // access ot our dictionary.
    _workerThread.Invoke(() => {
      _endpointsObservers.Add(observer, out _);
      // To avoid any further possibility of reentrancy while iterating over the dict,
      // make a copy of the keys
      var keys = _providerMap.Keys.ToArray();
      foreach (var endpointId in keys) {
        observer.OnNext(AddOrRemove<EndpointId>.OfAdd(endpointId));
      }
    });

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  public IDisposable Subscribe(EndpointId id, IObserver<EndpointState> observer) {
    IDisposable? disposable = null;
    ApplyTo(id, ep => disposable = ep.Subscribe(observer));

    return new ActionAsDisposable(() => {
      _workerThread.Invoke(() => {
        Utility.Exchange(ref disposable, null)?.Dispose();
      });
    });
  }

  private void ApplyTo(EndpointId id, Action<EndpointStateProvider> action) {
    _workerThread.Invoke(() => {
      if (!_providerMap.TryGetValue(id, out var ep)) {
        ep = new EndpointStateProvider(id, _workerThread);
        _providerMap.Add(id, ep);
        _endpointsObservers.OnNext(AddOrRemove<EndpointId>.OfAdd(id));
      }

      action(ep);
    });
  }
}


// public class EndpointState(CredentialsBase? credentials, StatusOr<SessionBase> session) {
//   public static EndpointState OfStatus(CredentialsBase? credentials, string status) {
//     var s = StatusOr<SessionBase>.OfStatus(status);
//     return new EndpointState(credentials, s);
//   }
//
//   public static EndpointState OfValue(CredentialsBase credentials, SessionBase sessionBase) {
//     var s = StatusOr<SessionBase>.OfValue(sessionBase);
//     return new EndpointState(credentials, s);
//   }
//
//   public CredentialsBase? Credentials = credentials;
//   public StatusOr<SessionBase> Session = session;
// }

// internal class EndpointStateProvider : IObservable<EndpointState>, IDisposable {
//   private readonly EndpointId _endpointId;
//   private readonly WorkerThread _workerThread;
//   private EndpointState _endpointState = EndpointState.OfStatus(null, "[Disconnected]");
//   private readonly ObserverContainer<EndpointState> _observerContainer = new();
//   private bool _disposed;
//
//   public EndpointStateProvider(EndpointId endpointId, WorkerThread workerThread) {
//     _endpointId = endpointId;
//     _workerThread = workerThread;
//   }
//
//   public void Dispose() {
//     if (_disposed) {
//       return;
//     }
//
//     _disposed = true;
//     // not even sure what to do.... maybe send an "end" to all of my existing observers?
//   }
//
//   public IDisposable Subscribe(IObserver<EndpointState> observer) {
//     _workerThread.Invoke(() => {
//       // New observer gets added to the collection and then notified of the current status.
//       _observerContainer.Add(observer, out _);
//       observer.OnNext(_endpointState);
//     });
//
//     return new ActionAsDisposable(() => {
//       _workerThread.Invoke(() => {
//         _observerContainer.Remove(observer, out _);
//       });
//     });
//   }
//
//   public void Reconnect() {
//     _workerThread.Invoke(() => {
//       if (_endpointState.Credentials == null) {
//         return;
//       }
//       SetCredentials(_endpointState.Credentials);
//     });
//   }
//
//   public void SetCredentials(CredentialsBase credentials) {
//     _workerThread.Invoke(() => {
//       try {
//         _endpointState = EndpointState.OfStatus(credentials, "Trying to connect");
//         _observerContainer.OnNext(_endpointState);
//
//         var sb = SessionBase.Of(credentials, _workerThread);
//         _endpointState = EndpointState.OfValue(credentials, sb);
//       } catch (Exception ex) {
//         _endpointState = EndpointState.OfStatus(credentials, ex.Message);
//       }
//       _observerContainer.OnNext(_endpointState);
//     });
//   }
// }

