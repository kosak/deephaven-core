using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Operations;
using Deephaven.DeephavenClient.ExcelAddIn.ViewModels;
using Deephaven.DeephavenClient.ExcelAddIn.Views;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

public static class DeephavenExcelFunctions {
  private static readonly ConnectionDialogViewModel ConnectionDialogViewModel = new ();
  private static readonly OperationManager OperationManager = new();

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Connect to Deephaven")]
  public static void ConnectToDeephaven() {
    var f = new ConnectionDialog(ConnectionDialogViewModel, (self, connectionString) => {
      OperationManager.Connect(connectionString);
      self.Close();
    });
    f.Show();
  }

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Reconnect")]
  public static void ReconnectToDeephaven() {
    // TODO(kosak): Thread safety for reading ConnectionString?
    OperationManager.Connect(ConnectionDialogViewModel.ConnectionString);
  }

  [ExcelFunction(Description = "Snapshots a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SNAPSHOT(string tableName) {
    const string functionName = "Deephaven.Client.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SNAPSHOT";
    ExcelObservableSource osrc = () => {
      var oc = new ObserverContainer();
      var op = new SnapshotOperation(tableName, oc);
      return new DeephavenExcelObservable(OperationManager, op, oc);
    };
    return ExcelAsyncUtil.Observe(functionName, tableName, osrc);
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SUBSCRIBE(string tableName) {
    const string functionName = "Deephaven.Client.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SUBSCRIBE";
    ExcelObservableSource osrc = () => {
      var oc = new ObserverContainer();
      var op = new SubscribeOperation(tableName, oc);
      return new DeephavenExcelObservable(OperationManager, op, oc);
    };
    return ExcelAsyncUtil.Observe(functionName, tableName, osrc);
  }
}
