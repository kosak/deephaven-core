using System.Diagnostics;
using Deephaven.DeephavenClient.ExcelAddIn.Operations;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class DeephavenStateManager {
  public static readonly DeephavenStateManager Instance = new();

  private readonly OperationManager _operationManager = new();

  public void Connect(string connectionString) {
    _operationManager.Connect(connectionString);
  }

  public IExcelObservable SnapshotTable(string tableName, TableFilter filter) {
    var oc = new ObserverContainer();
    var op = new SnapshotOperation(tableName, filter, oc);
    return new DeephavenHandler(_operationManager, op, oc);
  }

  public IExcelObservable SubscribeToTable(string tableName, TableFilter filter) {
    var oc = new ObserverContainer();
    var sh = new SubscribeOperation(tableName, filter, oc);
    return new DeephavenHandler(_operationManager, sh, oc);
  }
}
