global using BooleanChunk = Deephaven.ManagedClient.Chunk<bool>;
global using CharChunk = Deephaven.ManagedClient.Chunk<char>;
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
  public int Size { get; } = size;
}

public class Chunk<T> : Chunk {
  public static Chunk<T> Create(int size) {
    return new Chunk<T>(new T[size]);
  }

  public T[] Data { get; private set; }

  protected Chunk(T[] data) : base(data.Length) {
    Data = data;
  }
}
