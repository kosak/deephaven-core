namespace Deephaven.ManagedClient;

public static class ChunkMaker {
  private class MyVisitor(int chunkSize) : IColumnSourceVisitor {
    public Chunk? Result { get; private set; }

    public void Visit(IInt64ColumnSource cs) {
      Result = Int64Chunk.Create(chunkSize);
    }
  }

  public static Chunk CreateChunkFor(IColumnSource columnSource, int chunkSize) {
    var visitor = new MyVisitor(chunkSize);
    columnSource.AcceptVisitor(visitor);
    return visitor.Result;
  }
}
