namespace Deephaven.ManagedClient;

public abstract class Chunk {
  public int Size { get; }
}

public class GenericChunk<T> : Chunk {

}

