using System.Diagnostics;
using Deephaven.DeephavenClient;
using ExcelDna.Integration;

namespace Deephaven.Client.ExcelAddIn;

public class Doubtful {
  [ExcelFunction(Description = "instance method", IsThreadSafe = true)]
  public object SayHello2(string tableName) {
    return "does not work " + tableName;
  }
}

public static class ClientCache {
  private const string ServerAddress = "10.0.4.60:10000";

  private static readonly Task<DeephavenClient.Client> _instance;

  static ClientCache() {
    _instance = Nubbin();
  }

  private static async Task<DeephavenClient.Client> Nubbin() {
    var result = DeephavenClient.Client.Connect(ServerAddress, new ClientOptions());
    return result;
  }

  public static DeephavenClient.Client Instance => _instance.Result;
}

public static class MyFunctions {
  // [ExcelFunction(Description = "Fetches a table", IsThreadSafe = true)]
  // public static object FetchTable(string tableName) {
  //   // functionName and tableName are used as a key into a dictionary for reusing the same observable.
  //   const string functionName = "Deephaven.Client.ExcelAddIn.FetchTable";
  //   return ExcelAsyncUtil.Run(functionName, tableName, () => new FetchTableAsync(tableName));
  // }

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
      observer.OnNext($"Subscribing to \"{_tableName}\"...");
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
      SubscriptionHandle subHandle;
      lock (_sync) {
        _observers.Remove(observer);
        if (_observers.Count > 0) {
          return;
        }

        _subscribedToDeephaven = false;
        if (_subscriptionHandle == null) {
          return;
        }
        subHandle = _subscriptionHandle;
        _subscriptionHandle = null;
      }

      subHandle.Dispose();
    }

    private void PerformDeephavenSubscription() {
      try {
        var client = ClientCache.Instance;
        var th = client.Manager.FetchTable(_tableName);
        var subHandle = th.Subscribe(this);
        lock (_sync) {
          _subscriptionHandle = subHandle;
        }
      } catch (Exception ex) {
        var messages = ex.Message.Split('\n');
        SendStringsToObservers(messages);
      }
    }

    void ITickingCallback.OnTick(TickingUpdate update) {
      var current = update.Current;
      var numRows = current.NumRows;
      var numCols = current.NumCols;
      var result = new object?[numRows + 1, numCols];

      var headers = current.Schema.Names;
      for (var colIndex = 0; colIndex != numCols; ++colIndex) {
        result[0, colIndex] = headers[colIndex];

        var (col, nulls) = current.GetColumn(colIndex);
        for (var rowIndex = 0; rowIndex != numRows; ++rowIndex) {
          var temp = nulls[rowIndex] ? null : col.GetValue(rowIndex);
          // sad hack, wrong place, inefficient
          if (temp is DhDateTime dh) {
            temp = dh.DateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
          }
          result[rowIndex + 1, colIndex] = temp;
        }
      }

      var observersCopy = GetCopyOfObservers();
      foreach (var observer in observersCopy) {
        observer.OnNext(result);
      }
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

    private IExcelObserver[] GetCopyOfObservers() {
      lock (_sync) {
        return _observers.ToArray();
      }
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
