namespace Deephaven.ManagedClient;

public abstract class ArrayColumnSource : IColumnSource {
  protected readonly bool[] _nulls;

  public abstract void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags);
  public abstract void Accept(IColumnSourceVisitor visitor);
}

public sealed class ArrayColumnSource<T> : ArrayColumnSource, IColumnSource<T> {
  private readonly T[] _data;

  public override void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags) {
    var typedChunk = (Chunk<T>)dest;
    foreach (var (begin, end) in rows.Intervals) {
      for (var i = begin; i < end; ++i) {
        typedChunk.Data[i] = _data[i];
        if (nullFlags != null) {
          nullFlags.Data[i] = _nulls[i];
        }
      }
    }
  }

  public override void Accept(IColumnSourceVisitor visitor) {
    IColumnSource.Accept(this, visitor);
  }
}
