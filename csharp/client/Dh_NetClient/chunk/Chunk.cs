global using BooleanChunk = Deephaven.ManagedClient.Chunk<bool>;
global using StringChunk = Deephaven.ManagedClient.Chunk<string>;
global using CharChunk = Deephaven.ManagedClient.Chunk<char>;
global using ByteChunk = Deephaven.ManagedClient.Chunk<sbyte>;
global using Int16Chunk = Deephaven.ManagedClient.Chunk<System.Int16>;
global using Int32Chunk = Deephaven.ManagedClient.Chunk<System.Int32>;
global using Int64Chunk = Deephaven.ManagedClient.Chunk<System.Int64>;
global using FloatChunk = Deephaven.ManagedClient.Chunk<float>;
global using DoubleChunk = Deephaven.ManagedClient.Chunk<double>;
global using DateTimeChunk = Deephaven.ManagedClient.Chunk<System.DateTime>;
global using DateOnlyChunk = Deephaven.ManagedClient.Chunk<System.DateOnly>;
global using TimeOnlyChunk = Deephaven.ManagedClient.Chunk<System.TimeOnly>;

namespace Deephaven.ManagedClient;

public abstract class Chunk(int size) {
  public int Size { get; } = size;
}

public sealed class Chunk<T> : Chunk {
  public static Chunk<T> Create(int size) {
    return new Chunk<T>(new T[size]);
  }

  public T[] Data { get; }

  private Chunk(T[] data) : base(data.Length) {
    Data = data;
  }
}
