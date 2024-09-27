global using BooleanChunk = Deephaven.ManagedClient.Chunk<bool>;
global using StringChunk = Deephaven.ManagedClient.Chunk<string>;
global using CharChunk = Deephaven.ManagedClient.Chunk<char>;
global using ByteChunk = Deephaven.ManagedClient.Chunk<sbyte>;
global using Int16Chunk = Deephaven.ManagedClient.Chunk<System.Int16>;
global using Int32Chunk = Deephaven.ManagedClient.Chunk<System.Int32>;
global using Int64Chunk = Deephaven.ManagedClient.Chunk<System.Int64>;
global using FloatChunk = Deephaven.ManagedClient.Chunk<float>;
global using DoubleChunk = Deephaven.ManagedClient.Chunk<double>;
global using DhDateTimeChunk = Deephaven.ManagedClient.Chunk<Deephaven.ManagedClient.DhDateTime>;
global using LocalDateChunk = Deephaven.ManagedClient.Chunk<Deephaven.ManagedClient.LocalDate>;
global using LocalTimeChunk = Deephaven.ManagedClient.Chunk<Deephaven.ManagedClient.LocalTime>;

namespace Deephaven.ManagedClient;

public abstract class Chunk(int size) {
  public static Chunk CreateChunkFor(IColumnSource columnSource, int chunkSize) {
    var visitor = new ChunkMakerVisitor(chunkSize);
    columnSource.Accept(visitor);
    return visitor.Result!;
  }

  public int Size { get; } = size;
}

public sealed class Chunk<T> : Chunk {
  public static Chunk<T> Create(int size) {
    return new Chunk<T>(new T[size]);
  }

  public T[] Data { get; private set; }

  private Chunk(T[] data) : base(data.Length) {
    Data = data;
  }
}

class ChunkMakerVisitor(int chunkSize) :
  IColumnSourceVisitor<ICharColumnSource>,
  IColumnSourceVisitor<IByteColumnSource>,
  IColumnSourceVisitor<IInt16ColumnSource>,
  IColumnSourceVisitor<IInt32ColumnSource>,
  IColumnSourceVisitor<IInt64ColumnSource>,
  IColumnSourceVisitor<IFloatColumnSource>,
  IColumnSourceVisitor<IDoubleColumnSource>,
  IColumnSourceVisitor<IBooleanColumnSource>,
  IColumnSourceVisitor<IStringColumnSource>,
  IColumnSourceVisitor<ITimestampColumnSource>,
  IColumnSourceVisitor<ILocalDateColumnSource>,
  IColumnSourceVisitor<ILocalTimeColumnSource> {
  public Chunk? Result { get; private set; }

  public void Visit(ICharColumnSource cs) {
    Result = CharChunk.Create(chunkSize);
  }

  public void Visit(IByteColumnSource cs) {
    Result = ByteChunk.Create(chunkSize);
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

  public void Visit(IBooleanColumnSource cs) {
    Result = BooleanChunk.Create(chunkSize);
  }

  public void Visit(IStringColumnSource cs) {
    Result = StringChunk.Create(chunkSize);
  }

  public void Visit(ITimestampColumnSource cs) {
    Result = DhDateTimeChunk.Create(chunkSize);
  }

  public void Visit(ILocalDateColumnSource cs) {
    Result = LocalDateChunk.Create(chunkSize);
  }

  public void Visit(ILocalTimeColumnSource cs) {
    Result = LocalTimeChunk.Create(chunkSize);
  }

  public void Visit(IColumnSource cs) {
    throw new Exception($"Programming error: No visitor for type {cs.GetType().Name}");
  }
}
