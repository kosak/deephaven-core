using Apache.Arrow;
using Apache.Arrow.Types;

namespace Deephaven.ManagedClient;

public sealed class ArrowClientTable : ClientTable {
  public static ClientTable Create(Apache.Arrow.Table arrowTable) {
    // var schema = ArrowUtil.MakeDeephavenSchema(arrowTable.Schema);
    var rowSequence = RowSequence.CreateSequential(0, (UInt64)arrowTable.RowCount);

    var columnSources = new List<IColumnSource>();
    for (var i = 0; i != arrowTable.ColumnCount; ++i) {
      var col = arrowTable.Column(i);
      columnSources.Add(ArrowColumnSource.CreateFromColumn(col));
    }

    return new ArrowClientTable(arrowTable, rowSequence, columnSources.ToArray());
  }

  private readonly Apache.Arrow.Table _arrowTable;
  public override Schema Schema => _arrowTable.Schema;
  public override RowSequence RowSequence { get; }
  private readonly IColumnSource[] _columnSources;

  private ArrowClientTable(Apache.Arrow.Table arrowTable, RowSequence rowSequence,
    IColumnSource[] columnSources) {
    _arrowTable = arrowTable;
    RowSequence = rowSequence;
    _columnSources = columnSources;
  }

  public override IColumnSource GetColumn(int columnIndex) => _columnSources[columnIndex];

  public override Int64 NumRows => _arrowTable.RowCount;
  public override Int64 NumCols => _arrowTable.ColumnCount;
}
