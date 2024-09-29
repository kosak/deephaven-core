using System;
using System.Collections.Specialized;

namespace Deephaven.ManagedClient;

public abstract class RowSequence {
  public static RowSequence CreateSequential(UInt64 begin, UInt64 end) {
    return new SequentialRowSequence(begin, end);
  }

  public abstract bool Empty { get; }

  public abstract IEnumerable<(UInt64, UInt64)> Intervals { get; }
}

public sealed class SequentialRowSequence(UInt64 begin, UInt64 end) : RowSequence {
  public override bool Empty => begin == end;

  public override IEnumerable<(UInt64, UInt64)> Intervals {
    get {
      yield return (begin, end);
    }
  }
}

public sealed class BasicRowSequence((UInt64, UInt64)[] intervals) : RowSequence {
  public override bool Empty => intervals.Length == 0;

  public override IEnumerable<(UInt64, UInt64)> Intervals => intervals;
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

    return new BasicRowSequence(consolidatedIntervals.ToArray());
  }
}