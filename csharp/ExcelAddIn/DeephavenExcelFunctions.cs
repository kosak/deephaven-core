#define DEEPHAVEN_TESTING

using System.Diagnostics.CodeAnalysis;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Gui;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn;

public static class DeephavenExcelFunctions {
  private static class StateManagerHolder {
    public static readonly StateManager Value = StateManager.Create();
  }
  /// <summary>
  /// This adds a layer of indirection so the static StateManager is created later,
  /// when this property is called at runtime, rather than at class load time.
  /// </summary>
  private static StateManager StateManager => StateManagerHolder.Value;

  [ExcelCommand(MenuName = "Deephaven", MenuText = "&Control Panel")]
  public static void ShowConnectionsDialog() {
    ControlPanelManager.CreateAndShow(StateManager);
  }

#if DEEPHAVEN_TESTING
  [ExcelCommand(MenuName = "Debug", MenuText = "kosak testing")]
  public static void AddTestingConnections() {
    var id1 = new EndpointId("con1");
    var config1 = EndpointConfigBase.OfCore(id1, "10.0.4.109:10000");
    StateManager.SetConfig(config1);

    var id2 = new EndpointId("con2");
    var config2 = EndpointConfigBase.OfCorePlus(id2,
      "https://kosak-rc-the-og.int.illumon.com:8123/iris/connection.json",
      "iris", "iris", "iris", true);
    StateManager.SetConfig(config2);
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
#endif

  [ExcelFunction(Description = "Snapshots a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SNAPSHOT(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var tq, out var wh, out _)) {
      return ExcelError.ExcelErrorValue;
    }

    // For the StatusMonitor
    var description = MakeDescription("DEEPHAVEN_SNAPSHOT", tableDescriptor, filter, wantHeaders);

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SNAPSHOT";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    ExcelObservableSource eos = () => {
      var op = new SnapshotOperation(tq, wh, StateManager);
      return new ExcelOperation(description, op, StateManager);
    };
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SUBSCRIBE(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var tq, out var wh, out _)) {
      return ExcelError.ExcelErrorValue;
    }

    // For the StatusMonitor
    var description = MakeDescription("DEEPHAVEN_SUBSCRIBE", tableDescriptor, filter, wantHeaders);

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SUBSCRIBE";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    ExcelObservableSource eos = () => {
      var op = new SubscribeOperation(tq, wh, StateManager);
      return new ExcelOperation(description, op, StateManager);
    };
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }


  [ExcelFunction(Description = "Gets table headers", IsThreadSafe = true)]
  public static object DEEPHAVEN_HEADERS(string tableDescriptor) {
    if (!TableTriple.TryParse(tableDescriptor, out var tt, out _)) {
      return ExcelError.ExcelErrorValue;
    }

    // For the StatusMonitor
    var description = $"DEEPHAVEN_HEADERS(\"{tableDescriptor}\")";

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_HEADERS";
    var parms = new[] { tableDescriptor };
    ExcelObservableSource eos = () => {
      var op = new TableHeadersOperation(tt, StateManager);
      return new ExcelOperation(description, op, StateManager);
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

  private static string MakeDescription(string function, string tableDescriptor, object filter,
    object wantHeaders) {
    var args = new List<string> { $"\"{tableDescriptor}\"" };

    var filterMissing = filter is ExcelMissing or ExcelEmpty;
    var wantHeadersMissing = wantHeaders is ExcelMissing or ExcelEmpty;

    args.Add(filterMissing ? "" : $"\"{filter}\"");
    args.Add(wantHeadersMissing ? "" : wantHeaders.ToString()!);

    // Now trim. A little wasteful but simple.
    if (wantHeadersMissing) {
      args.RemoveAt(args.Count - 1);

      if (filterMissing) {
        args.RemoveAt(args.Count - 1);
      }
    }

    return $"{function}({string.Join(',', args)})";
  }
}
