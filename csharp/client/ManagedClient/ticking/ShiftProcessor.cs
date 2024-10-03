namespace Deephaven.ManagedClient;
public static class ShiftProcessor {
  public static IEnumerable<(Interval, UInt64 destKey)> AnalyzeShiftData(RowSequence firstIndex,
    RowSequence lastIndex, RowSequence destIndex) {
    if (firstIndex.IsEmpty) {
      if (!lastIndex.IsEmpty || !destIndex.IsEmpty) {
        throw new ArgumentException($"Expected all indices to be empty but first={firstIndex.IsEmpty}, " +
                                    $"last={lastIndex.IsEmpty}, dest={destIndex.IsEmpty}");
        yield break;
      }
    }

    // Loop twice: once in the forward direction (applying negative shifts), and once in the reverse
    // direction (applying positive shifts). Because we don't have a reverse iterator,
    // we save up the reverse tuples for processing in a separate step.

    var positiveShifts = new List<(Interval, UInt64 destKey)>();
    using (var firstIter = firstIndex.Elements.GetEnumerator()) {
      using var lastIter = lastIndex.Elements.GetEnumerator();
      using var destIter = destIndex.Elements.GetEnumerator();
      while (firstIter.MoveNext()) {
        if (!lastIter.MoveNext() || !destIter.MoveNext()) {
          throw new ArgumentException("Sequences not of same Size");
        }

        var first = firstIter.Current;
        var last = lastIter.Current;
        var dest = destIter.Current;

        var interval = Interval.Of(first, checked(last + 1));

        if (dest >= first) {
          positiveShifts.Add((interval, dest));
          continue;
        }

        yield return (interval, dest);
      }
    }

    for (var i = positiveShifts.Count; i-- > 0;) {
      yield return positiveShifts[i];
    }
  }
}
