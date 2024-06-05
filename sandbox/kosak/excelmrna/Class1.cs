
using ExcelDna.Integration;
using ExcelDna.Registration;

using System.Linq;
using ExcelDna.Integration;
using ExcelDna.Registration;
using ExcelDna.Registration.Utils;

public static class MyFunctions
{
    [ExcelFunction(Description = "My first .NET function")]
    public static string SayHelloOld(string name)
    {
        return "Hello " + name;
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