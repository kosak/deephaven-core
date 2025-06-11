using System.Diagnostics.CodeAnalysis;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Gui;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using ExcelDna.Integration;
using ExcelDna.IntelliSense;

namespace Deephaven.ExcelAddIn;

public class StartupHelper : IExcelAddIn {
  public void AutoOpen() {
    IntelliSenseServer.Install();
    _ = StateManagerSingleton.Instance;
  }
  public void AutoClose() {
    IntelliSenseServer.Uninstall();
  }
}

public static class StateManagerSingleton {
  private static readonly object _sync = new();
  private static StateManager? _instance = null;

  public static StateManager Instance {
    get {
      lock (_sync) {
        if (_instance == null) {
          _instance = StateManager.Create();
        }
        return _instance;
      }
    }
  }
}

public static class DeephavenExcelFunctions {
  [ExcelCommand(MenuName = "Deephaven", MenuText = "&Control Panel")]
  public static void ShowConnectionsDialog() {
    ControlPanelManager.CreateAndShow(StateManagerSingleton.Instance);
  }

  private const string TableDescriptorDescription =
    "A string in the form con:pq/table.  con refers to a configured connection. " +
    "If con: is left out, the default connection is used. " +
    "pq refers to named Persistent Query. If pq/ is left out, a Community Core server is assumed.";

  private const string FilterDescription =
    "A filter expression in server query syntax. Can be left blank. Example: \"Volume > 5000 || Price < 100.0\"";

  private const string WantHeadersDescription =
    "If a header row should be included in the output. Valid values are true or false. " +
    "Can be left blank. To provide headers separately from the data, use =DEEPHAVEN_HEADERS";

  [ExcelFunction(Description = "Snapshots a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SNAPSHOT(
    [ExcelArgument(Name="tableDescriptor", Description = TableDescriptorDescription)]
    string tableDescriptor,
    [ExcelArgument(Name="filter", Description = FilterDescription)]
    object filter,
    [ExcelArgument(Name="wantHeaders", Description = WantHeadersDescription)]
    object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var tq, out var wh, out _)) {
      return ExcelError.ExcelErrorValue;
    }

    // For the StatusMonitor
    var description = MakeDescription("DEEPHAVEN_SNAPSHOT", tableDescriptor, filter, wantHeaders);

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SNAPSHOT";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    ExcelObservableSource eos = () => {
      var op = new SnapshotOperation(tq, wh, StateManagerSingleton.Instance);
      return new ExcelOperation(description, op, StateManagerSingleton.Instance);
    };
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SUBSCRIBE(
    [ExcelArgument(Name="tableDescriptor", Description = TableDescriptorDescription)]
    string tableDescriptor,
    [ExcelArgument(Name="filter", Description = FilterDescription)]
    object filter,
    [ExcelArgument(Name="wantHeaders", Description = WantHeadersDescription)]
    object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var tq, out var wh, out _)) {
      return ExcelError.ExcelErrorValue;
    }

    // For the StatusMonitor
    var description = MakeDescription("DEEPHAVEN_SUBSCRIBE", tableDescriptor, filter, wantHeaders);

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SUBSCRIBE";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    ExcelObservableSource eos = () => {
      var op = new SubscribeOperation(tq, wh, StateManagerSingleton.Instance);
      return new ExcelOperation(description, op, StateManagerSingleton.Instance);
    };
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  [ExcelFunction(Description = "Gets table headers", IsThreadSafe = true)]
  public static object DEEPHAVEN_HEADERS(
    [ExcelArgument(Name="tableDescriptor", Description = TableDescriptorDescription)]
    string tableDescriptor) {
    if (!TableTriple.TryParse(tableDescriptor, out var tt, out _)) {
      return ExcelError.ExcelErrorValue;
    }

    // For the StatusMonitor
    var description = $"DEEPHAVEN_HEADERS(\"{tableDescriptor}\")";

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_HEADERS";
    var parms = new[] { tableDescriptor };
    ExcelObservableSource eos = () => {
      var op = new TableHeadersOperation(tt, StateManagerSingleton.Instance);
      return new ExcelOperation(description, op, StateManagerSingleton.Instance);
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
