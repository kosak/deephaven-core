using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Interop.TestApi;
using ExcelDna.Integration;
using ExcelAddIn;

namespace Deephaven.DeephavenClient.ExcelAddIn;

public static partial class InvokeWithFullPath {
  [LibraryImport(@"C:\Users\kosak\Desktop\laboratory\dhsimple2.dll")]
  public static partial void zamboni_doadd(int a, int b, out int result);
}

public static partial class InvokeWithBareName {
  [LibraryImport(@"dhsimple2")]
  public static partial void zamboni_doadd(int a, int b, out int result);
}

public static partial class InvokeWithDllExtension {
  [LibraryImport(@"dhsimple2.dll")]
  public static partial void zamboni_doadd(int a, int b, out int result);
}

public static class DeephavenExcelFunctions {
  private static readonly ConnectionDialogViewModel ConnectionDialogViewModel = new ();

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Connect to Deephaven")]
  public static void ConnectToDeephaven() {
    var f = new ConnectionDialog(ConnectionDialogViewModel, (Form self, string connectionString) => {
      DeephavenStateManager.Instance.Connect(connectionString);
      self.Close();
    });
    f.Show();
  }

  [ExcelFunction(Description = "Test simple call to add", IsThreadSafe = true)]
  public static object TEST4_ADD(int a, int b) {
    try {
      BasicInteropInteractions.deephaven_dhcore_interop_testapi_BasicInteropInteractions_Add(a, b, out var result);
      return result;
    } catch (Exception e) {
      var ts = e.TargetSite != null ? e.TargetSite.ToString() : "ts12";
      return $"v8a:{e.Message} {ts}";
    }
  }

  [ExcelFunction(Description = "Test simple call to add", IsThreadSafe = true)]
  public static object TEST_FullPath(int a, int b) {
    try {
      InvokeWithFullPath.zamboni_doadd(a, b, out var result);
      return result;
    } catch (Exception e) {
      var ts = e.TargetSite != null ? e.TargetSite.ToString() : "ts12";
      return $"v8b:{e.Message} {ts}";
    }
  }

  [ExcelFunction(Description = "Test simple call to add", IsThreadSafe = true)]
  public static object TEST_BareName(int a, int b) {
    try {
      InvokeWithBareName.zamboni_doadd(a, b, out var result);
      return result;
    } catch (Exception e) {
      var ts = e.TargetSite != null ? e.TargetSite.ToString() : "ts12";
      return $"v8b:{e.Message} {ts}";
    }
  }

  [ExcelFunction(Description = "Test simple call to add", IsThreadSafe = true)]
  public static object TEST_DllExtension(int a, int b) {
    try {
      InvokeWithDllExtension.zamboni_doadd(a, b, out var result);
      return result;
    } catch (Exception e) {
      var ts = e.TargetSite != null ? e.TargetSite.ToString() : "ts12";
      return $"v8b:{e.Message} {ts}";
    }
  }

  [ExcelFunction(Description = "Snapshots a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SNAPSHOT(string tableName) {
    var dsm = DeephavenStateManager.Instance;
    const string functionName = "Deephaven.Client.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SNAPSHOT";
    return ExcelAsyncUtil.Observe(functionName, tableName, () => dsm.SnapshotTable(tableName, TableFilter.Default));
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SUBSCRIBE(string tableName) {
    var dsm = DeephavenStateManager.Instance;
    const string functionName = "Deephaven.Client.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SUBSCRIBE";
    return ExcelAsyncUtil.Observe(functionName, tableName, () => dsm.SubscribeToTable(tableName, TableFilter.Default));
  }
}

public class ConnectionDialogViewModel {
  private string _connectionString = "10.0.4.106:10000";

  public event EventHandler? ConnectionStringChanged;

  public string ConnectionString {
    get => _connectionString;
    set {
      if (_connectionString == value) {
        return;
      }
      _connectionString = value;
      OnConnectionStringChanged();
    }
  }

  private void OnConnectionStringChanged() {
    ConnectionStringChanged?.Invoke(this, EventArgs.Empty);
  }
}
