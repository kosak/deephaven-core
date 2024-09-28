using Apache.Arrow;

namespace Deephaven.ManagedClient;

public class Int64ArrowColumnSource : IInt64ColumnSource {
  public static Int64ArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = new Int64Array[chunkedArray.ArrayCount];
    for (var i = 0; i < chunkedArray.ArrayCount; i++) {
      arrays[i] = (Int64Array)chunkedArray.ArrowArray(i);
    }

    return new Int64ArrowColumnSource(arrays);
  }

  private Int64Array[] _arrays;

  private Int64ArrowColumnSource(Int64Array[] arrays) {
    _arrays = arrays;
  }
}
