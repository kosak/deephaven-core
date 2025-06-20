using Apache.Arrow;
using Apache.Arrow.Types;

namespace Deephaven.ManagedClient;

public sealed class ArrowClientTable : IClientTable {
  public static IClientTable Create(Apache.Arrow.Table arrowTable) {
    // var schema = ArrowUtil.MakeDeephavenSchema(arrowTable.Schema);
    var rowSequence = RowSequence.CreateSequential(Interval.Of(0, (UInt64)arrowTable.RowCount));

    var columnSources = new List<IColumnSource>();
    for (var i = 0; i != arrowTable.ColumnCount; ++i) {
      var col = arrowTable.Column(i);
      columnSources.Add(ArrowColumnSource.CreateFromColumn(col));
    }

    return new ArrowClientTable(arrowTable, rowSequence, columnSources.ToArray());
  }

  private readonly Apache.Arrow.Table _arrowTable;
  public Schema Schema => _arrowTable.Schema;
  public RowSequence RowSequence { get; }
  private readonly IColumnSource[] _columnSources;

  private ArrowClientTable(Apache.Arrow.Table arrowTable, RowSequence rowSequence,
    IColumnSource[] columnSources) {
    _arrowTable = arrowTable;
    RowSequence = rowSequence;
    _columnSources = columnSources;
  }

  public void Dispose() {
    // Nothing to do.
  }

  public IColumnSource GetColumn(int columnIndex) => _columnSources[columnIndex];

  public Int64 NumRows => _arrowTable.RowCount;
  public Int64 NumCols => _arrowTable.ColumnCount;
}
