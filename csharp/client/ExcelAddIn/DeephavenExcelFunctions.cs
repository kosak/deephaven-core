using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.ExcelAddIn.ViewModels;
using Deephaven.DeephavenClient.Interop.TestApi;
using ExcelDna.Integration;
using ExcelAddIn;

namespace Deephaven.DeephavenClient.ExcelAddIn;

public static class DeephavenExcelFunctions {
  private static readonly ConnectionDialogViewModel ConnectionDialogViewModel = new ();

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Connect to Deephaven")]
  public static void ConnectToDeephaven() {
    var f = new ConnectionDialog(ConnectionDialogViewModel, (self, connectionString) => {
      DeephavenStateManager.Instance.Connect(connectionString);
      self.Close();
    });
    f.Show();
  }

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Reconnect")]
  public static void ReconnectToDeephaven() {
    // Thread safety?
    DeephavenStateManager.Instance.Connect(ConnectionDialogViewModel.ConnectionString);
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
