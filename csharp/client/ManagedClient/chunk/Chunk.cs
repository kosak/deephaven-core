namespace Deephaven.ManagedClient;

public abstract class Chunk(int size) {
  public int Size { get; } = size;
}

public class GenericChunk<T> : Chunk {
  public T[] Data { get; private set; }

  protected GenericChunk(T[] data) : base(data.Length) {
    Data = data;
  }
}

public class BooleanChunk : GenericChunk<bool> {
  public static BooleanChunk Create(int size) {
    return new BooleanChunk(new bool[size]);
  }

  protected BooleanChunk(bool[] data) : base(data) {
  }
}

public class Int32Chunk : GenericChunk<Int32> {
  public static Int32Chunk Create(int size) {
    return new Int32Chunk(new Int32[size]);
  }

  protected Int32Chunk(Int32[] data) : base(data) {
  }
}

public class Int64Chunk : GenericChunk<Int64> {
  public static Int64Chunk Create(int size) {
    return new Int64Chunk(new Int64[size]);
  }

  protected Int64Chunk(Int64[] data) : base(data) {
  }
}
