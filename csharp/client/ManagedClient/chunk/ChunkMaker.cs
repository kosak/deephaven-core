namespace Deephaven.ManagedClient;

public static class ChunkMaker {
  private class MyVisitor(int chunkSize) : IColumnSourceVisitor {
    public Chunk? Result { get; private set; }

    public void Visit(ICharColumnSource cs) {
      Result = CharChunk.Create(chunkSize);
    }

    public void Visit(IInt16ColumnSource cs) {
      Result = Int16Chunk.Create(chunkSize);
    }

    public void Visit(IInt32ColumnSource cs) {
      Result = Int32Chunk.Create(chunkSize);
    }

    public void Visit(IInt64ColumnSource cs) {
      Result = Int64Chunk.Create(chunkSize);
    }

    public void Visit(IFloatColumnSource cs) {
      Result = FloatChunk.Create(chunkSize);
    }

    public void Visit(IDoubleColumnSource cs) {
      Result = DoubleChunk.Create(chunkSize);
    }

    public void Visit(ITimestampColumnSource cs) {
      Result = DhDateTimeChunk.Create(chunkSize);
    }
  }

  public static Chunk CreateChunkFor(IColumnSource columnSource, int chunkSize) {
    var visitor = new MyVisitor(chunkSize);
    columnSource.AcceptVisitor(visitor);
    if (visitor.Result == null) {
      throw new Exception($"Programming error: Result not set for type {columnSource.GetType().Name}");
    }

    return visitor.Result;
  }
}
