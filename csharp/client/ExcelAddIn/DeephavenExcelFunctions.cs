using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Operations;
using Deephaven.DeephavenClient.ExcelAddIn.Views;
using Deephaven.ExcelAddIn.Operations;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.ViewModels;
using ExcelAddIn.views;
using ExcelDna.Integration;
using static System.Net.WebRequestMethods;

namespace Deephaven.ExcelAddIn;

public static class DeephavenExcelFunctions {
  private static readonly ConnectionDialogViewModel ConnectionDialogViewModel = new ();
  private static readonly EnterpriseConnectionDialogViewModel EnterpriseConnectionDialogViewModel = new ();
  private static readonly OperationManager OperationManager = new();

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Connect to Deephaven")]
  public static void ConnectToDeephaven() {
    var f = new ConnectionDialog(ConnectionDialogViewModel, (self, connectionString) => {
      OperationManager.Connect(connectionString);
      self.Close();
    });
    f.Show();
  }

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Connect to Deephaven Enterprise")]
  public static void ConnectToDeephavenEnterprise() {
    var f = new EnterpriseConnectionDialog(EnterpriseConnectionDialogViewModel,
      (self, jsonUrl, username, password, operateAs, pqName) => {
      OperationManager.ConnectToEnterprise(jsonUrl, username, password, operateAs, pqName);
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
  public static object DEEPHAVEN_SNAPSHOT(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var ftd, out var wh, out var errorText)) {
      return errorText;
    }

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SNAPSHOT";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    ExcelObservableSource eos = () => new SnapshotOperation(ftd, wh, OperationManager);
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SUBSCRIBE(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var ftd, out var wh, out string errorText)) {
      return errorText;
    }
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SUBSCRIBE";
    ExcelObservableSource eos = () => new SubscribeOperation(ftd, wh, OperationManager);
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  private static bool TryInterpretCommonArgs(string tableDescriptor, object filter, object wantHeaders,
    out FilteredTableDescriptor? filteredTableDescriptorResult, out bool wantHeadersResult, out string errorText) {
    filteredTableDescriptorResult = null;
    wantHeadersResult = false;
    errorText = "";
    if (!InterpretOptional.TryInterpretAs(filter, "", out var filterText)) {
      errorText = "Can't interpret FILTER argument";
      return false;
    }

    if (!FilteredTableDescriptor.TryParse(filterText, out filteredTableDescriptorResult)) {
      return false;
    }

    if (!InterpretOptional.TryInterpretAs(wantHeaders, false, out wantHeadersResult)) {
      errorText = "Can't interpret WANT_HEADERS argument";
      return false;
    }
    return true;
  }
}
