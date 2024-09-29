using Apache.Arrow;
using Apache.Arrow.Types;

namespace Deephaven.ManagedClient;

public sealed class ArrowClientTable : ClientTable {
  public static ClientTable Create(Apache.Arrow.Table arrowTable) {
    // var schema = ArrowUtil.MakeDeephavenSchema(arrowTable.Schema);
    var rowSequence = RowSequence.CreateSequential(0, arrowTable.RowCount);

    var columnSources = new List<IColumnSource>();
    for (var i = 0; i != arrowTable.ColumnCount; ++i) {
      var col = arrowTable.Column(i);
      columnSources.Add(MakeColumnSource(col));
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

  private class MyVisitor(ChunkedArray chunkedArray) :
    IArrowTypeVisitor<UInt16Type>,
    IArrowTypeVisitor<Int16Type>,
    IArrowTypeVisitor<Int32Type>,
    IArrowTypeVisitor<Int64Type>,
    IArrowTypeVisitor<FloatType>,
    IArrowTypeVisitor<DoubleType> {
    public IColumnSource Result { get; private set; }

    public void Visit(UInt16Type type) {
      Result = CharArrowColumnSource.OfChunkedArray(chunkedArray);
    }

    public void Visit(Int16Type type) {
      Result = Int16ArrowColumnSource.OfChunkedArray(chunkedArray);
    }

    public void Visit(Int32Type type) {
      Result = Int32ArrowColumnSource.OfChunkedArray(chunkedArray);
    }

    public void Visit(Int64Type type) {
      Result = Int64ArrowColumnSource.OfChunkedArray(chunkedArray);
    }

    public void Visit(FloatType type) {
      Result = FloatArrowColumnSource.OfChunkedArray(chunkedArray);
    }

    public void Visit(DoubleType type) {
      Result = DoubleArrowColumnSource.OfChunkedArray(chunkedArray);
    }

    public void Visit(IArrowType type) {
      throw new Exception($"type {type.Name} is not supported");
    }
  }

  private static IColumnSource MakeColumnSource(Column column) {
    var visitor = new MyVisitor(column.Data);
    column.Type.Accept(visitor);
    return visitor.Result;
  }
}
