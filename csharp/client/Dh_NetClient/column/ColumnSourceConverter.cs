namespace Deephaven.Dh_NetClient;

public static class ColumnSourceConverter {
  public static Array ToArray(IColumnSource columnSource, int numRows) {
    var visitor = new ToArrayVisitor(numRows);
    columnSource.Accept(visitor);
    return visitor.Result;
  }

  private class ToArrayVisitor(int numRows) : IColumnSourceVisitor {
    public Array Result = Array.Empty<object>();

    public void Visit(IColumnSource cs) {
      throw new Exception($"ToArray not supported for type {cs.GetType().Name}");
    }
  }
}
