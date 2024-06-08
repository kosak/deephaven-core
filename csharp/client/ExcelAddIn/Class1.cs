using System.Diagnostics;
using Deephaven.DeephavenClient;
using ExcelDna.Integration;

namespace Deephaven.Client.ExcelAddIn;

public static class MyFunctions {
  [ExcelFunction(Description = "My first .NET function")]
  public static string SayHello(string name) {
    return "Hello " + name;
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object Subscribe(string tableName) {
    // functionName and tableName are used as a key into a dictionary for reusing the same observable.
    const string functionName = "Deephaven.Client.ExcelAddIn.Subscribe";
    return ExcelAsyncUtil.Observe(functionName, tableName, () => new SubscribeObservable(tableName));
  }

  private class SubscribeObservable : IExcelObservable, ITickingCallback {
    private readonly string _tableName;
    private readonly object _sync = new();
    private readonly HashSet<IExcelObserver> _observers = new();
    private bool _subscribedToDeephaven;
    private SubscriptionHandle? _subscriptionHandle;

    public SubscribeObservable(string tableName) {
      _tableName = tableName;
    }

    public IDisposable Subscribe(IExcelObserver observer) {
      AddObserver(observer);
      return new ActionDisposable(() => RemoveObserver(observer));
    }

    private void AddObserver(IExcelObserver observer) {
      lock (_sync) {
        _observers.Add(observer);
        if (_subscribedToDeephaven) {
          return;
        }

        _subscribedToDeephaven = true;
      }

      Task.Run(PerformDeephavenSubscription);
    }

    private void RemoveObserver(IExcelObserver observer) {
      lock (_sync) {
        _observers.Remove(observer);
        if (_observers.Count > 0) {
          return;
        }

        _subscribedToDeephaven = false;
      }

    }

    private void PerformDeephavenSubscription() {
      try {
        const string deephavenServerAddress = "localhost:10000";
        var client = DeephavenClient.Client.Connect(deephavenServerAddress, new ClientOptions());
        var th = client.Manager.FetchTable(_tableName);
        var subHandle = th.Subscribe(this);
        lock (_sync) {
          _subscriptionHandle = subHandle;
        }
      } catch (Exception ex) {
        SendStringsToObservers(new[]{ex.Message});
      }
    }

    private int _nextIndex = 0;

    void ITickingCallback.OnTick(TickingUpdate update) {
      int nextIndex;
      lock (_sync) {
        nextIndex = ++_nextIndex;
      }
      SendStringsToObservers(new[]{nextIndex + ": got an update hi"});
    }

    void ITickingCallback.OnFailure(string message) {
      SendStringsToObservers(new[]{message});
    }

    private void SendStringsToObservers(string[] strings) {
      var result = new object[strings.Length, 1];
      for (var i = 0; i != strings.Length; ++i) {
        result[i, 0] = strings[i];
      }

      IExcelObserver[] observersCopy;
      lock (_sync) {
        observersCopy = _observers.ToArray();
      }

      foreach (var observer in observersCopy) {
        observer.OnNext(result);
      }
    }

    private void SendExceptionToObservers(Exception ex) {
      IExcelObserver[] observersCopy;
      lock (_sync) {
        observersCopy = _observers.ToArray();
      }

      foreach (var observer in observersCopy) {
        // observer.OnError(ex);
        observer.OnNext("stupid");
      }

      // maybe do oncompleted and also deephaven unsubscribe
    }

    private class ActionDisposable : IDisposable {
      private readonly Action _disposeAction;

      public ActionDisposable(Action disposeAction) {
        _disposeAction = disposeAction;
      }

      public void Dispose() {
        _disposeAction();
        Debug.WriteLine("Disposed");
      }
    }
  }
}
