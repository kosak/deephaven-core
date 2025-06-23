namespace Deephaven.Dh_NetClient;

public static class ArrowArrayConverter {
  public static Apache.Arrow.IArrowArray ColumnSourceToArray(IColumnSource columnSource, Int64 numRows) {
    var visitor = new ColumnSourceToArrayVisitor(numRows);
    columnSource.Accept(visitor);
    return visitor.Result!;
  }

  private class ColumnSourceToArrayVisitor(Int64 numRows) : IColumnSourceVisitor {
    public Apache.Arrow.IArrowArray? Result = null;

    public void Visit(IColumnSource cs) {
      throw new NotImplementedException($"{cs.GetType().Name}");
    }
  }
}
