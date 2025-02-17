using System.Diagnostics.CodeAnalysis;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Operations;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn;

public static class DeephavenExcelFunctions {
  private static readonly StateManager StateManager = new();

  [ExcelFunction(Description = "Snapshots a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SNAPSHOT(string tableDescriptor, object filter, object wantHeaders) {
    // if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var tq, out var wh, out var errorText)) {
    //   return errorText;
    // }
    throw new Exception("sad");
  }
}
