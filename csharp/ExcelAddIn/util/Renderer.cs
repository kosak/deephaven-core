using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Util;

internal static class Renderer {
  public static object?[,] Render(IClientTable table, bool wantHeaders) {
    var numRows = table.NumRows;
    var numCols = table.NumCols;
    var effectiveNumRows = wantHeaders ? numRows + 1 : numRows;
    var result = new object?[effectiveNumRows, numCols];

    var fields = table.Schema.FieldsList;
    for (var colIndex = 0; colIndex != numCols; ++colIndex) {
      var destIndex = 0;
      if (wantHeaders) {
        result[destIndex++, colIndex] = fields[colIndex].Name;
      }

      var col = table.GetColumn(colIndex);
      foreach (var (value, isNull) in GrabValues(col, numRows)) {
        // sad hack, wrong place, inefficient
        object valueToUse = value is DhDateTime dh
          ? dh.DateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
          : value;

        result[destIndex++, colIndex] = valueToUse;
      }
    }

    return result;
  }

  private static IEnumerable<ValueTuple<object, bool>> GrabValues(IColumnSource col, long size) {
    yield return ValueTuple.Create("mega sad", false);
  }
}
