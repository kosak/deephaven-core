using Apache.Arrow;

namespace Deephaven.ManagedClient;

public class Int64ArrowColumnSource : IInt64ColumnSource {
  public static Int64ArrowColumnSource OfChunkedArray(ChunkedArray chunkedArray) {
    var arrays = new Int64Array[chunkedArray.ArrayCount];
    for (var i = 0; i < chunkedArray.ArrayCount; i++) {
      arrays[i] = (Int64Array)chunkedArray.ArrowArray(i);
    }

    return new Int64ArrowColumnSource(arrays);
  }

  private Int64Array[] _arrays;

  private Int64ArrowColumnSource(Int64Array[] arrays) {
    _arrays = arrays;
  }

  public void FillChunk(RowSequence rows, Chunk destData, BooleanChunk? nullFlags) {
    if (rows.Empty) {
      return;
    }

    // This algorithm is a little tricky because the source data and RowSequence are both
    // segmented, perhaps in different ways.
    var typedDest = (Int64Chunk)destData;
    var destSpan = new Span<Int64>(typedDest.Data);

    var srcIterator = new ZamboniIterator<Int64>(_arrays);
    var nullDestp = nullFlags != null ? new Span<bool>(nullFlags.Data) : null;

    foreach (var (reqBeginConst, reqEnd) in rows.Intervals) {
      var reqBegin = reqBeginConst;
      while (true) {
        var reqLength = (reqEnd - reqBegin).ToIntExact();
        if (reqLength == 0) {
          return;
        }

        srcIterator.Advance(reqBegin);
        var amountToCopy = Math.Min(srcIterator.AvailableInCurrentSegment, reqLength);
        srcIterator.Values.CopyTo(destSpan);
        destSpan = destSpan.Slice(amountToCopy);

        reqBegin += amountToCopy;
        // and then handle nulls
      }
    }
  }

  public void AcceptVisitor(IColumnSourceVisitor visitor) {
    visitor.Visit(this);
  }
}

public class ZamboniIterator<T> where T : struct, IEquatable<T> {
  private readonly IReadOnlyList<PrimitiveArray<T>> _arrays;
  private int _arrayIndex = -1;
  private Int64 _segmentOffset = 0;
  private Int64 _segmentBegin = 0;
  private Int64 _segmentEnd = 0;


  public ZamboniIterator(IReadOnlyList<PrimitiveArray<T>> arrays) {
    _arrays = arrays;
  }

  public void Advance(Int64 start) {
    while (true) {
      if (start < _segmentBegin) {
        throw new Exception($"Programming error: Can't go backwards from {_segmentBegin} to {start}");
      }

      if (start < _segmentEnd) {
        // satisfiable with current segment
        _segmentBegin = start;
        return;
      }

      // Go to next array slice (or the first one, if this is the first call to Advance)
      ++_arrayIndex;
      if (_arrayIndex >= _arrays.Count) {
        throw new Exception($"Ran out of src data before processing all of RowSequence");
      }

      _segmentBegin = _segmentEnd;
      _segmentEnd = _segmentBegin + _arrays[_arrayIndex ].Length;
      _segmentOffset = _segmentBegin;
    }
  }

  public int AvailableInCurrentSegment => (_segmentEnd - _segmentBegin).ToIntExact();

  public ReadOnlySpan<T> Values => _arrays[_arrayIndex].Values.Slice((_segmentBegin - _segmentOffset).ToIntExact());
}