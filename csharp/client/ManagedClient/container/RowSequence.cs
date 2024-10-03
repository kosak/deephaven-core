using System;

namespace Deephaven.ManagedClient;

public abstract class RowSequence {
  public static RowSequence CreateEmpty() => SequentialRowSequence.EmptyInstance;

  public static RowSequence CreateSequential(Interval interval) {
    return new SequentialRowSequence(interval);
  }

  public bool Empty => Count == 0;
  public abstract UInt64 Count { get; }

  public abstract IEnumerable<Interval> Intervals { get; }

  public abstract RowSequence Take(UInt64 size);
  public abstract RowSequence Drop(UInt64 size);
}

sealed class SequentialRowSequence(Interval interval) : RowSequence {
  public static readonly SequentialRowSequence EmptyInstance = new(Interval.Empty);

  public override UInt64 Count => interval.Count;

  public override IEnumerable<Interval> Intervals {
    get {
      yield return interval;
    }
  }

  public override RowSequence Take(UInt64 size) {
    var sizeToUse = Math.Min(size, Count);
    return new SequentialRowSequence(interval with { End = interval.Begin + sizeToUse });
  }

  public override RowSequence Drop(UInt64 size) {
    var sizeToUse = Math.Min(size, Count);
    return new SequentialRowSequence(interval with { Begin = interval.Begin + sizeToUse });
  }
}

sealed class BasicRowSequence : RowSequence {
  public static BasicRowSequence Create(IEnumerable<(ulong, ulong)> intervals) {
    var intervalsArray = intervals.ToArray();
    UInt64 size = 0;
    foreach (var e in intervalsArray) {
      size += e.Item2 - e.Item1;
    }

    return new BasicRowSequence(intervalsArray, 0, 0, size);
  }

  private readonly (UInt64, UInt64)[] _intervals;
  private readonly int _startIndex = 0;
  private readonly UInt64 _startOffset = 0;
  public override UInt64 Size { get; }

  private BasicRowSequence((ulong, ulong)[] intervals, int startIndex, ulong startOffset, ulong size) {
    _intervals = intervals;
    _startIndex = startIndex;
    _startOffset = startOffset;
    Size = size;
  }

  public override RowSequence Take(UInt64 size) =>
    new BasicRowSequence(_intervals, _startIndex, _startOffset, Size);

  public override RowSequence Drop(UInt64 size) {
    return DropHelper(size).Last().Item3!;
  }

  public override IEnumerable<(UInt64, UInt64)> Intervals =>
      DropHelper(Size).Where(elt => elt.Item3 == null).Select(elt => (elt.Item1, elt.Item2));

  private IEnumerable<(UInt64, UInt64, RowSequence?)> DropHelper(UInt64 size) {
    if (size == 0) {
      yield return (0, 0, this);
      yield break;
    }

    var currentIndex = _startIndex;
    var currentOffset = _startOffset;
    var remainingSizeToDrop = Math.Min(size, Size);
    var finalSize = Size - remainingSizeToDrop;
    while (remainingSizeToDrop != 0) {
      var current = _intervals[currentIndex];
      var entrySize = current.Item2 - current.Item1;
      if (currentOffset == entrySize) {
        ++currentIndex;
        currentOffset = 0;
        continue;
      }

      var entryRemaining = entrySize - currentOffset;
      var amountToConsume = Math.Min(entryRemaining, remainingSizeToDrop);
      var begin = current.Item1 + currentOffset;
      var end = begin + amountToConsume;
      currentOffset += amountToConsume;
      remainingSizeToDrop -= amountToConsume;
      yield return (begin, end, null);
    }

    var result = new BasicRowSequence(_intervals, currentIndex, currentOffset, finalSize);
    yield return (0, 0, result);
  }
}

public class RowSequenceBuilder {
  private readonly List<(UInt64, UInt64)> _intervals = new();

  /// <summary>
  /// Adds the half-open interval [begin, end) to the RowSequence. The added interval need not be
  /// disjoint with the other intervals.
  /// </summary>
  /// <param name="begin">The closed start of the interval</param>
  /// <param name="end">The open end of the interval</param>
  public void AddInterval(UInt64 begin, UInt64 end) {
    if (begin == end) {
      return;
    }
    _intervals.Add((begin, end));
  }

  /// <summary>
  /// Adds 'key' to the RowSequence. If the key is already present, does nothing.
  /// </summary>
  /// <param name="key">The key</param>
  public void Add(UInt64 key) {
    AddInterval(key, checked(key + 1));
  }

  /// <summary>
  /// Builds the RowSequence
  /// </summary>
  /// <returns>The built RowSequence</returns>
  public RowSequence Build() {
    _intervals.Sort((lhs, rhs) => lhs.Item1.CompareTo(rhs.Item1));

    var consolidatedIntervals = new List<(UInt64, UInt64)>();

    using var it = _intervals.GetEnumerator();
    if (it.MoveNext()) {
      var lastInterval = it.Current;
      while (it.MoveNext()) {
        var thisInterval = it.Current;
        if (lastInterval.Item2 >= thisInterval.Item1) {
          lastInterval.Item2 = Math.Max(lastInterval.Item2, thisInterval.Item2);
          continue;
        }

        consolidatedIntervals.Add(lastInterval);
        lastInterval = thisInterval;
      }

      consolidatedIntervals.Add(lastInterval);
    }

    return BasicRowSequence.Create(consolidatedIntervals);
  }
}
