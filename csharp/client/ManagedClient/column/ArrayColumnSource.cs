namespace Deephaven.ManagedClient;

public abstract class ArrayColumnSource : IColumnSource {

}

public class ArrayColumnSource<T> : ArrayColumnSource, IColumnSource<T> {
  private readonly T[] _data;
  private readonly bool[] _nulls;

  public void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags) {
    foreach (var interval in rows.Intervals) {

    }
    throw new NotImplementedException();
  }

  public void Accept(IColumnSourceVisitor visitor) {

    visitor.Visit(this);
  }
}
