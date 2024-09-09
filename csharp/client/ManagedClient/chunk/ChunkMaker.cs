﻿namespace Deephaven.ManagedClient;

public static class ChunkMaker {
  public static Chunk CreateChunkFor(IColumnSource columnSource, int chunkSize) {
    var visitor = new ChunkMakerVisitor(chunkSize);
    columnSource.Accept(visitor);
    return visitor.Result!;
  }

  private class ChunkMakerVisitor(int chunkSize) :
    IColumnSourceVisitor<ICharColumnSource>,
    IColumnSourceVisitor<IByteColumnSource>,
    IColumnSourceVisitor<IInt16ColumnSource>,
    IColumnSourceVisitor<IInt32ColumnSource>,
    IColumnSourceVisitor<IInt64ColumnSource>,
    IColumnSourceVisitor<IFloatColumnSource>,
    IColumnSourceVisitor<IDoubleColumnSource>,
    IColumnSourceVisitor<IBooleanColumnSource>,
    IColumnSourceVisitor<IStringColumnSource>,
    IColumnSourceVisitor<IDateTimeColumnSource>,
    IColumnSourceVisitor<IDateOnlyColumnSource>,
    IColumnSourceVisitor<ITimeOnlyColumnSource> {
    public Chunk? Result { get; private set; }

    public void Visit(ICharColumnSource cs) => Doit(cs);
    public void Visit(IByteColumnSource cs) => Doit(cs);
    public void Visit(IInt16ColumnSource cs) => Doit(cs);
    public void Visit(IInt32ColumnSource cs) => Doit(cs);
    public void Visit(IInt64ColumnSource cs) => Doit(cs);
    public void Visit(IFloatColumnSource cs) => Doit(cs);
    public void Visit(IDoubleColumnSource cs) => Doit(cs);
    public void Visit(IBooleanColumnSource cs) => Doit(cs);
    public void Visit(IStringColumnSource cs) => Doit(cs);
    public void Visit(IDateTimeColumnSource cs) => Doit(cs);
    public void Visit(IDateOnlyColumnSource cs) => Doit(cs);
    public void Visit(ITimeOnlyColumnSource cs) => Doit(cs);

    public void Visit(IColumnSource cs) {
      throw new Exception($"Programming error: No visitor for type {cs.GetType().Name}");
    }

    private void Doit<T>(IColumnSource<T> _) {
      Result = Chunk<T>.Create(chunkSize);
    }
  }
}
