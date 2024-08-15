using System.Diagnostics;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Operations;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.ViewModels;
using Deephaven.ExcelAddIn.Views;
using ExcelAddIn.views;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn;

public static class DeephavenExcelFunctions {
  private static readonly ConnectionDialogViewModel ConnectionDialogViewModel = new ();
  private static readonly EnterpriseConnectionDialogViewModel EnterpriseConnectionDialogViewModel = new ();
  private static readonly StateManager StateManager = new();

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Connections")]
  public static void ManagedConnections() {
    var f = new ConnectionManagerDialog();
    f.Show();
  }

  [ExcelFunction(Description = "Snapshots a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SNAPSHOT(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var td, out var filt, out var wh, out var errorText)) {
      return errorText;
    }

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SNAPSHOT";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    ExcelObservableSource eos = () => new SnapshotOperation(td!, filt, wh, StateManager);
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SUBSCRIBE(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var td, out var filt, out var wh, out string errorText)) {
      return errorText;
    }
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SUBSCRIBE";
    ExcelObservableSource eos = () => new SubscribeOperation(td, filt, wh, StateManager);
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  private static bool TryInterpretCommonArgs(string tableDescriptor, object filter, object wantHeaders,
    out TableDescriptor? tableDescriptorResult, out string filterResult, out bool wantHeadersResult, out string errorText) {
    filterResult = "";
    wantHeadersResult = false;
    if (!TableDescriptor.TryParse(tableDescriptor, out tableDescriptorResult, out errorText)) {
      return false;
    }

    if (!ExcelDnaHelpers.TryInterpretAs(filter, "", out filterResult)) {
      errorText = "Can't interpret FILTER argument";
      return false;
    }


    if (!ExcelDnaHelpers.TryInterpretAs(wantHeaders, false, out wantHeadersResult)) {
      errorText = "Can't interpret WANT_HEADERS argument";
      return false;
    }
    return true;
  }
}
