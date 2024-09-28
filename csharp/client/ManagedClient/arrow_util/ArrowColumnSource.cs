using Apache.Arrow;

namespace Deephaven.ManagedClient;

public class GenericArrowColumnSource<TCRTP, U> {
  public static TCRTP OfChunkedArray(ChunkedArray chunkedArray) {
    throw new NotImplementedException("wow");
  }
}

public class Int64ArrowColumnSource : GenericArrowColumnSource<Int64ArrowColumnSource, Int64Array>, IInt64ColumnSource {

}
