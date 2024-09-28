using IInt64ColumnSource2 = Deephaven.ManagedClient.INumericColumnSource<System.Int64>;

using Int64ArrowColumnSource = Deephaven.ManagedClient.GenericArrowColumnSource<
  IInt64ColumnSource2,
  Apache.Arrow.Int64Array>;

namespace Deephaven.ManagedClient;

public class GenericArrowColumnSource<T,U> {
}
