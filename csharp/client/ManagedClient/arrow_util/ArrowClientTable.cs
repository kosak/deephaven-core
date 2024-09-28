using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Apache.Arrow;

namespace Deephaven.ManagedClient;

public sealed class ArrowClientTable : ClientTable {
  static ClientTable Create(Apache.Arrow.Table arrowTable) {
    var schema = ArrowUtil.MakeDeephavenSchema(arrowTable.Schema);
    var rowSequence = RowSequence.CreateSequential(0, arrowTable.RowCount);

    var columnSources = new List<ColumnSource>();
    for (var i = 0; i != arrowTable.ColumnCount; ++i) {
      var col = arrowTable.Column(i);
      columnSources.Add(MakeColumnSource(col));
    }

    return new ArrowClientTable(arrowTable, schema, rowSequence, columnSources.ToArray());
  }

  private readonly Apache.Arrow.Table _arrowTable;
  public override Schema Schema { get; }
  public override RowSequence RowSequence { get; }
  private readonly ColumnSource[] _columnSources;

  private ArrowClientTable(Apache.Arrow.Table arrowTable, Schema schema, RowSequence rowSequence,
    ColumnSource[] columnSources) {
    _arrowTable = arrowTable;
    Schema = schema;
    RowSequence = rowSequence;
    _columnSources = columnSources;
  }

  public override ColumnSource GetColumn(int columnIndex) => _columnSources[columnIndex];

  public override Int64 NumRows => _arrowTable.RowCount;
  public override Int64 NumCols => _arrowTable.ColumnCount;
}
