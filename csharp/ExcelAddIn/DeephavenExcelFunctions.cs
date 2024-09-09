using System.Diagnostics.CodeAnalysis;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Gui;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn;

public static class DeephavenExcelFunctions {
  private static readonly StateManager StateManager = new();

  [ExcelCommand(MenuName = "Deephaven", MenuText = "&Connections")]
  public static void ShowConnectionsDialog() {
    EndpointManagerDialogManager.CreateAndShow(StateManager);
  }

  [ExcelCommand(MenuName = "Deephaven", MenuText = "&Status Monitor")]
  public static void ShowStatusDialog() {
    StatusMonitorDialogManager.CreateAndShow(StateManager);
  }

  [ExcelCommand(MenuName = "Debug", MenuText = "kosak Core local")]
  public static void AddKosakConnection() {
    var id = new EndpointId("con1");
    var config = EndpointConfigBase.OfCore(id, "10.0.4.109:10000");
    StateManager.SetConfig(config);
  }

  // See if this works to set note or comment
  // ExcelReference? refCaller = XlCall.Excel(XlCall.xlfCaller) as ExcelReference;
  // and then

  // [ExcelFunction(IsThreadSafe = false)]
  // public static void AddCommentToCell(int rowIndex, int colIndex, string commentText) {
  //   Excel.Application app = (Excel.Application)ExcelDnaUtil.Application;
  //   Excel.Worksheet ws = (Excel.Worksheet)app.ActiveSheet;
  //   Excel.Range cell = (Excel.Range)ws.Cells[rowIndex, colIndex];
  //
  //   // Add the comment
  //   cell.AddComment(commentText);
  // }

  [ExcelFunction(Description = "Test function", IsThreadSafe = true)]
  public static object DEEPHAVEN_TEST() {
    return ExcelError.ExcelErrorNull;
  }

  [ExcelFunction(Description = "Snapshots a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SNAPSHOT(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var tq, out var wh, out var errorText)) {
      return errorText;
    }

    // For the StatusMonitor
    var description = MakeDescription("DEEPHAVEN_SNAPSHOT", tableDescriptor, filter, wantHeaders);

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SNAPSHOT";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    var retryKey = new TableTriple(tq.EndpointId, tq.PqName, tq.TableName);
    ExcelObservableSource eos = () => {
      var op = new SnapshotOperation(tq, wh, StateManager);
      return new ExcelOperation(description, retryKey, op, StateManager);
    };
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SUBSCRIBE(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var tq, out var wh, out string errorText)) {
      return errorText;
    }

    // For the StatusMonitor
    var description = MakeDescription("DEEPHAVEN_SUBSCRIBE", tableDescriptor, filter, wantHeaders);

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SUBSCRIBE";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    var retryKey = new TableTriple(tq.EndpointId, tq.PqName, tq.TableName);
    ExcelObservableSource eos = () => {
      var op = new SubscribeOperation(tq, wh, StateManager);
      return new ExcelOperation(description, retryKey, op, StateManager);
    };
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  private static bool TryInterpretCommonArgs(string tableDescriptor, object filter, object wantHeaders,
    [NotNullWhen(true)]out TableQuad? tableQuadResult, out bool wantHeadersResult, out string errorText) {
    tableQuadResult = null;
    wantHeadersResult = false;
    if (!TableTriple.TryParse(tableDescriptor, out var tt, out errorText)) {
      return false;
    }

    if (!ExcelDnaHelpers.TryInterpretAs(filter, "", out var condition)) {
      errorText = "Can't interpret FILTER argument";
      return false;
    }


    if (!ExcelDnaHelpers.TryInterpretAs(wantHeaders, false, out wantHeadersResult)) {
      errorText = "Can't interpret WANT_HEADERS argument";
      return false;
    }

    tableQuadResult = new TableQuad(tt.EndpointId, tt.PqName, tt.TableName, condition);
    return true;
  }

  private static string MakeDescription(string function, string tableDescriptor, object filter, object wantHeaders) {
    var args = new List<string> { $"\"{tableDescriptor}\"" };
    if (filter is not ExcelMissing) {
      args.Add(filter.ToString()!);
    }
    if (wantHeaders is not ExcelMissing) {
      args.Add(wantHeaders.ToString()!);
    }
    return $"{function}({string.Join(',', args)})";
  }
}
