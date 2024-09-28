namespace Deephaven.ManagedClient;

public abstract class RowSequence {
  public static RowSequence CreateSequential(Int64 begin, Int64 end) {
    return new SequentialRowSequence(begin, end);
  }

  public abstract bool Empty { get; }

  public abstract IEnumerable<(Int64, Int64)> Intervals { get; }
}

public sealed class SequentialRowSequence(Int64 begin, Int64 end) : RowSequence {
  public override bool Empty => begin == end;

  public override IEnumerable<(long, long)> Intervals {
    get {
      yield return (begin, end);
    }
  }
}
