global using ICharColumnSource = Deephaven.ManagedClient.IColumnSource<char>;
global using IByteColumnSource = Deephaven.ManagedClient.IColumnSource<sbyte>;
global using IInt16ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int16>;
global using IInt32ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int32>;
global using IInt64ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int64>;
global using IFloatColumnSource = Deephaven.ManagedClient.IColumnSource<float>;
global using IDoubleColumnSource = Deephaven.ManagedClient.IColumnSource<double>;
global using IBooleanColumnSource = Deephaven.ManagedClient.IColumnSource<bool>;
global using IStringColumnSource = Deephaven.ManagedClient.IColumnSource<string>;
global using ITimestampColumnSource = Deephaven.ManagedClient.IColumnSource<Deephaven.ManagedClient.DhDateTime>;
global using ILocalDateColumnSource = Deephaven.ManagedClient.IColumnSource<Deephaven.ManagedClient.LocalDate>;
global using ILocalTimeColumnSource = Deephaven.ManagedClient.IColumnSource<Deephaven.ManagedClient.LocalTime>;

namespace Deephaven.ManagedClient;

public interface IColumnSource {
  void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags);
  void Accept(IColumnSourceVisitor visitor);

  /// <summary>
  /// Part of acyclic visitor pattern
  /// </summary>
  public static void Accept<T>(T columnSource, IColumnSourceVisitor visitor)
    where T : class, IColumnSource {
    if (visitor is IColumnSourceVisitor<T> typedVisitor) {
      typedVisitor.Visit(columnSource);
    } else {
      visitor.Visit(columnSource);
    }
  }

  public static void Copy(IColumnSource src, int srcIndex, IMutableColumnSource dest, int destIndex, int numItems) {
    var chunkSize = Math.Min(8192, numItems);
    var chunk = ChunkMaker.CreateChunkFor(src, chunkSize);
    var nulls = BooleanChunk.Create(chunkSize);
    var itemsRemaining = numItems;
    while (itemsRemaining != 0) {
      var amountToCopyThisTime = Math.Min(itemsRemaining, chunkSize);
      var srcRs = RowSequence.CreateSequential(Interval.OfStartAndSize((UInt64)srcIndex, (UInt64)amountToCopyThisTime));
      src.FillChunk(srcRs, chunk, nulls);
      var destRs = RowSequence.CreateSequential(Interval.OfStartAndSize((UInt64)destIndex, (UInt64)amountToCopyThisTime));
      dest.FillFromChunk(destRs, chunk, nulls);

      srcIndex += amountToCopyThisTime;
      destIndex += amountToCopyThisTime;
      itemsRemaining -= amountToCopyThisTime;
    }
  }
}

public interface IColumnSource<T> : IColumnSource {

}

public interface IMutableColumnSource : IColumnSource {
  void FillFromChunk(RowSequence rows, Chunk src, BooleanChunk? nullFlags);
}

public interface IMutableColumnSource<T> : IMutableColumnSource, IColumnSource<T> {

}

/// <summary>
/// Part of acyclic visitor pattern
/// </summary>
public interface IColumnSourceVisitor {
  void Visit(IColumnSource cs);
}

/// <summary>
/// Part of acyclic visitor pattern
/// </summary>
public interface IColumnSourceVisitor<in T> : IColumnSourceVisitor where T : IColumnSource {
  void Visit(T cs);
}
