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

    var chunkSize = checked((int)Math.Min(numRows, 1024));
    var nulls = Chunk<bool>.Create(chunkSize);

    var endIndex = (UInt64)numRows;

    for (var colIndex = 0; colIndex != numCols; ++colIndex) {
      UInt64 currentIndex = 0;
      var destIndex = destStartIndex;

      var col = table.GetColumn(colIndex);
      var chunk = ChunkMaker.CreateChunkFor(col, chunkSize);
      var dateTimeChunk = chunk as Chunk<DhDateTime>;

      var sizeToCopy = Math.Min(endIndex - currentIndex, (UInt64)chunkSize);
      var rows = RowSequence.CreateSequential(Interval.OfStartAndSize(currentIndex, sizeToCopy));
      col.FillChunk(rows, chunk, nulls);
      currentIndex += sizeToCopy;

      for (UInt64 i = 0; i != sizeToCopy; ++i) {
        // Assume null
        object? value = null;
        if (!nulls.Data[i]) {
          if (dateTimeChunk != null) {
            // Special case for DhDateTimes: format them as readable strings
            value = dateTimeChunk.Data[i].DateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
          } else {
            value = chunk.GetBoxedElement((int)i);
          }
        }

        result[destIndex++, colIndex] = value;
      }
    }

    return result;
  }
}
