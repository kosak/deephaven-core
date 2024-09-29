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
    IArrowTypeVisitor<Int8Type>,
    IArrowTypeVisitor<Int16Type>,
    IArrowTypeVisitor<Int32Type>,
    IArrowTypeVisitor<Int64Type>,
    IArrowTypeVisitor<FloatType>,
    IArrowTypeVisitor<DoubleType>,
    IArrowTypeVisitor<BooleanType>,
    IArrowTypeVisitor<StringType>,
    IArrowTypeVisitor<TimestampType>,
    IArrowTypeVisitor<Date64Type>,
    IArrowTypeVisitor<Time64Type> {
    public IColumnSource? Result { get; private set; }

    public void Visit(UInt16Type type) {
      Result = new CharArrowColumnSource(chunkedArray);
    }

    public void Visit(Int8Type type) {
      Result = new ByteArrowColumnSource(chunkedArray);
    }

    public void Visit(Int16Type type) {
      Result = new Int16ArrowColumnSource(chunkedArray);
    }

    public void Visit(Int32Type type) {
      Result = new Int32ArrowColumnSource(chunkedArray);
    }

    public void Visit(Int64Type type) {
      Result = new Int64ArrowColumnSource(chunkedArray);
    }

    public void Visit(FloatType type) {
      Result = new FloatArrowColumnSource(chunkedArray);
    }

    public void Visit(DoubleType type) {
      Result = new DoubleArrowColumnSource(chunkedArray);
    }

    public void Visit(BooleanType type) {
      Result = new BooleanArrowColumnSource(chunkedArray);
    }

    public void Visit(StringType type) {
      Result = new StringArrowColumnSource(chunkedArray);
    }

    public void Visit(TimestampType type) {
      Result = new TimestampArrowColumnSource(chunkedArray);
    }

    public void Visit(Date64Type type) {
      Result = new LocalDateArrowColumnSource(chunkedArray);
    }

    public void Visit(Time64Type type) {
      Result = new LocalTimeArrowColumnSource(chunkedArray);
    }

    public void Visit(IArrowType type) {
      throw new Exception($"type {type.Name} is not supported");
    }
  }

  private static IColumnSource MakeColumnSource(Column column) {
    var visitor = new MyVisitor(column.Data);
    column.Type.Accept(visitor);
    if (visitor.Result == null) {
      throw new Exception($"No result set for {column.Data.DataType}");
    }
    return visitor.Result;
  }
}
