
using ExcelDna.Integration;
using ExcelDna.Registration;

using System.Linq;
using ExcelDna.Integration;
using ExcelDna.Registration;
using ExcelDna.Registration.Utils;
using System.Diagnostics;

public static class MyFunctions
{
    [ExcelFunction(Description = "My first .NET function")]
    public static string SayHelloOld(string name)
    {
        return "Hello " + name;
    }

    [ExcelFunction(Description = "My nth,kth .NET function")]
    public static object[,] IAmACamera(string name, int rows, int cols) {
      var result = new object[rows, cols];
      for (var j = 0; j != rows; ++j) {
        for (var i = 0; i != cols; ++i) {
          if ((j % 2) == 0) {
            result[j, i] = $"qqq {name} {j}{i}";
          } else {
            result[j, i] = j * 333 + i;
          }
        }
      }

      return result;
    }


  [ExcelFunction(Description = "My second .NET function")]
    public static object Concat2(object[,] values)
    {
        string result = "";
        int rows = values.GetLength(0);
        int cols = values.GetLength(1);
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                object value = values[i, j];
                result += value.ToString();
            }
        }
        return result;
    }


    [ExcelCommand(MenuName = "Test", MenuText = "Range Set")]
    public static void RangeSet()
    {
        dynamic xlApp = ExcelDnaUtil.Application;

        xlApp.Range["F1"].Value = "Testing 1... 2... 3... 4";
    }

  //The main function that is exposed to Excel.
  [ExcelFunction(Description = "My first qqqqqqq.NET function")]
  public static object DownloadStringFromURL(string url) {
    var functionName = nameof(DownloadStringFromURL);
    var parameters = new object[] { url };
    HttpClient myHttpClient = new HttpClient();

    return ExcelAsyncUtil.RunTask(functionName, parameters, async () => {
      //The actual asyncronous block of code to execute.
      return await myHttpClient.GetStringAsync(url);
    });

  }

  [ExcelFunction(Description = "Provides a thread safe ticking clock", IsThreadSafe = true)]
  public static object dnaRtdClock_IExcelObservableThreadSafe(string param) {
    string functionName = nameof(dnaRtdClock_IExcelObservableThreadSafe);
    object paramInfo = param; // could be one parameter passed in directly, or an object array of all the parameters: new object[] {param1, param2}
    return ExcelAsyncUtil.Observe(functionName, paramInfo, () => new ExcelObservableClock());
  }

  class ExcelObservableClock : IExcelObservable {
    Timer _timer;
    List<IExcelObserver> _observers;

    public ExcelObservableClock() {
      _timer = new Timer(timer_tick, null, 0, 100);
      _observers = new List<IExcelObserver>();
    }

    public IDisposable Subscribe(IExcelObserver observer) {
      _observers.Add(observer);
      var toast = Populate("loveshack1");
      observer.OnNext(toast);
      return new ActionDisposable(() => _observers.Remove(observer));
    }

    void timer_tick(object _) {
      var toast = Populate("never");
      foreach (var obs in _observers)
        obs.OnNext(toast);
    }

    private object[,] Populate(string what) {
      const int rows = 10000;
      const int cols = 8;
      var result = new object[rows, cols];
      for (var j = 0; j != rows; ++j) {
        for (var i = 0; i < cols; ++i) {
          result[j, i] = DateTime.Now.Microsecond + j * 1000 + i;
        }
      }
      result[0, 0] = what + DateTime.Now.ToString("HH:mm:ss.fff");
      return result;
    }

    class ActionDisposable : IDisposable {
      Action _disposeAction;
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

namespace excelmrna {
  public class AddIn : IExcelAddIn {
    public void AutoOpen() {
      RegisterFunctions();
    }

    public void AutoClose() {
    }

    public void RegisterFunctions() {
      // There are various options for wrapping and transforming your functions
      // See the Source\Samples\Registration.Sample project for a comprehensive example
      // Here we just change the attribute before registering the functions
      ExcelRegistration.GetExcelFunctions()
                       .Select(UpdateHelpTopic)
                       .RegisterFunctions();

    }

    public ExcelFunctionRegistration UpdateHelpTopic(ExcelFunctionRegistration funcReg) {
      funcReg.FunctionAttribute.HelpTopic = "http://www.bing.com";
      return funcReg;
    }
  }

  public class Functions {
    [ExcelFunction(HelpTopic = "http://www.google.com")]
    public static object SayHello() {
      return "Hello!!!";
    }
  }
}