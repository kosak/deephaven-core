using Deephaven.ManagedClient;
using System.Drawing;

namespace Deephaven.ExcelAddIn.Util;

internal static class Renderer {
  public static object?[,] Render(IClientTable table, bool wantHeaders) {
    var numRows = table.NumRows;
    var numCols = table.NumCols;
    var effectiveNumRows = wantHeaders ? numRows + 1 : numRows;
    var result = new object?[effectiveNumRows, numCols];

    var fields = table.Schema.FieldsList;
    var destStartIndex = 0;
    if (wantHeaders) {
      for (var colIndex = 0; colIndex != numCols; ++colIndex) {
        result[0, colIndex] = fields[colIndex].Name;
      }

      destStartIndex = 1;
    }

    var chunkSize = checked((int)Math.Min(numRows, 16384));
    var nulls = Chunk<bool>.Create(chunkSize);

    var endIndex = (UInt64)numRows;

    for (var colIndex = 0; colIndex != numCols; ++colIndex) {
      UInt64 currentIndex = 0;
      var destIndex = destStartIndex;

      var col = table.GetColumn(colIndex);
      var adaptorMaker = new AdaptorMaker();
      col.Accept(adaptorMaker);
      var adaptor = adaptorMaker.Adaptor;

      while (currentIndex < endIndex) {
        var sizeToCopy = Math.Min(endIndex - currentIndex, (UInt64)chunkSize);
        var rows = RowSequence.CreateSequential(Interval.OfStartAndSize(currentIndex, sizeToCopy));
        col.FillChunk(rows, adaptor.SrcChunk, nulls);
        adaptor.AdaptData();
        currentIndex += sizeToCopy;

        var destData = adaptor.DestData;
        for (UInt64 i = 0; i != sizeToCopy; ++i) {
          result[destIndex++, colIndex] = destData[i];
        }
      }
    }

    return result;
  }

  private class AdaptorMaker : IColumnSourceVisitor {
  }

  private class Adaptor {
    protected readonly object[] _destData;

  }

  private class Adaptor<T> : Adaptor {
    private readonly Chunk<T> _srcChunk;
    private readonly Func<T, object> _converter;

    // Assume null, which we render as empty string.
    object? value = "";
      if (!nulls.Data[i]) {
      if (dateTimeChunk != null) {
        // Special case for DhDateTimes: format them as readable strings
        value = dateTimeChunk.Data[i].DateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
      } else {
        value = chunk.GetBoxedElement((int) i);
      }
    }









  }
}
