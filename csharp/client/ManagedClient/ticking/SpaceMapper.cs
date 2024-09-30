using C5;
using System;

namespace Deephaven.ManagedClient;

public class SpaceMapper {
  private readonly TreeSet<UInt64> _set = new();

  /// <summary>
  /// Adds 'keys' (specified in key space) to the map, and returns the positions (in position
  /// space) of those keys after insertion. 'keys' are required to not already been in the map.
  /// The algorithm behaves as though all the keys are inserted in the map and then
  /// 'ConvertKeysToIndices' is called.
  /// <example>
  /// <ul>
  /// <li>SpaceMapper currently holds[100 300]</li>
  /// <li>AddKeys called with [1, 2, 200, 201, 400, 401]</li>
  /// <li>SpaceMapper final state is [1, 2, 100, 200, 201, 300, 400, 401]</li>
  /// <li>The returned result is [0, 1, 3, 4, 6, 7]</li>
  /// </ul>
  /// </example>
  /// </summary>
  /// <param name="keys"></param>
  /// <returns></returns>
  /// <exception cref="NotImplementedException"></exception>
  public RowSequence AddKeys(RowSequence keys) {
    var builder = new RowSequenceBuilder();
    foreach (var interval in keys.Intervals) {
      var indexSpaceRange = AddRange(interval.Item1, interval.Item2);
      builder.AddInterval(indexSpaceRange.Item1, indexSpaceRange.Item2);
    }
    return builder.Build();
  }

  /// <summary>
  /// Adds the keys (represented in key space) in the half-open interval [begin_key, end_key_) to the set.
  /// The keys must not already exist in the set.If they do, an exception is thrown.
  /// </summary>
  /// <param name="beginKey">The first key to insert</param>
  /// <param name="endKey">One past the last key to insert</param>
  /// <returns>The added keys, represented as a range in index space</returns>
  (UInt64, UInt64) AddRange(UInt64 beginKey, UInt64 endKey) {
    var temp9 = _set.CountSpeed;
    var initialCardinality = _set.Count;
    var rangeSize = (endKey - beginKey).ToIntExact();
    var temp = new UInt64[rangeSize];
    for (var i = 0; i != rangeSize; ++i) {
      temp[i] = beginKey + (UInt64)i;
    }
    _set.AddSorted(temp);
    var finalCardinality = _set.Count;
    var cardinalityChange = finalCardinality - initialCardinality;
    if (cardinalityChange != rangeSize) {
      throw new Exception($"Range [{beginKey},{endKey}) has size {rangeSize} but set only changed from cardinality {initialCardinality} to {finalCardinality}. This means duplicates were inserted");
    }

    var stupid = _set.IndexingSpeed;
    var index = _set.LastIndexOf(beginKey);
    if (index < 0) {
      throw new Exception("Programming error: item not found?");
    }

    return ((UInt64)index, (UInt64)index + (UInt64)rangeSize);
  }

  /// <summary>
  /// Removes the keys in the half-open interval[begin_key, end_key_) from the set.
  /// It is ok if some or all of the keys do not exist in the set.
  /// </summary>
  /// <param name="beginKey">The first key to remove</param>
  /// <param name="endKey">One past the last key to remove</param>
  /// <returns>The rank of beginKey</returns>
  public UInt64 EraseRange(UInt64 beginKey, UInt64 endKey) {
    var result = ZeroBasedRank(beginKey);
    _set.RemoveRangeFromTo(beginKey, endKey);
    return result;
  }

  /// <summary>
  /// Delete all the keys that currently exist in the range [begin_key, end_key).
  /// Call that set of deleted keys K.The cardinality of K might be smaller than
  /// (end_key - begin_key) because not all keys in that range are expected to be present.
  ///
  /// Then calculate a new set of keys KNew = { k ∈ K | (k - begin_key + dest_key) }
  /// and insert this new set of keys into the map.
  ///
  /// This has the effect of offsetting all the existing keys by (dest_key - begin_key)
  /// </summary>
  ///
  /// <param name="beginKey">The start of the range of keys</param>
  /// <param name="endKey">One past the end of the range of keys</param>
  /// <param name="destKey">The start of the target range to move keys to</param>
  /// <exception cref="NotImplementedException"></exception>
  public void ApplyShift(UInt64 beginKey, UInt64 endKey, UInt64 destKey) {
    // Note that [begin_key, end_key) is potentially a superset of the keys we have.
    // We need to remove all our keys in the range [begin_key, end_key),
    // and then, for each key k that we removed, add a new key (k - begin_key + dest_key).

    // We start by building the new ranges
    // As we scan the keys in our set, we build this vector which contains contiguous ranges.
    var newRanges = new List<(UInt64, UInt64)>();
    var subset = _set.RangeFromTo(beginKey, endKey);
    foreach (var item in subset) {
      var itemOffset = item - beginKey;
      var newKey = destKey + itemOffset;
      if (newRanges.Count > 0 && newRanges[^1].Item2 == newKey) {
        // This key is contiguous with the last range, so extend it by one.
        var back = newRanges[^1];
        newRanges[^1] = (back.Item1, back.Item2 + 1);  // aka newKey + 1
      } else {
        // This key is not contiguous with the last range (or there is no last range), so
        // start a new range here having size 1.
        newRanges.Add((newKey, newKey + 1));
      }
    }

    // Shifts do not change the size of the set. So, note the original size as a sanity check.
    var originalSize = _set.Count;
    _set.RemoveRangeFromTo(beginKey, endKey);
    foreach (var entry in newRanges) {
      _ = AddRange(entry.Item1, entry.Item2);
    }
    var finalSize = _set.Count;

    if (originalSize != finalSize) {
      throw new Exception($"Unexpected SpaceMapper size change: from {originalSize} to {finalSize}");
    }
  }

  /**
   * Looks up 'keys' (specified in key space) in the map, and returns the positions (in position
   * space) of those keys.
   */
  public RowSequence ConvertKeysToIndices(RowSequence keys) {
    if (keys.Size == 0) {
      return RowSequence.CreateEmpty();
    }

    var builder = new RowSequenceBuilder();
    foreach (var interval in keys.Intervals) {
      var size = interval.Item2 - interval.Item1;
      var subset = _set.RangeFromTo(interval.Item1, interval.Item2);
      if ((UInt64)subset.Count != size) {
        throw new Exception($"Of the {size} keys specified in [{interval.Item1},{interval.Item2}) the set only contains {subset.Count} keys");
      }

      var nextRank = ZeroBasedRank(interval.Item1);
      builder.AddInterval(nextRank, nextRank + size);
    }
    return builder.Build();
  }

  public int Cardinality() {
    return _set.Count;
  }

  public UInt64 ZeroBasedRank(UInt64 value) {
    var index = _set.LastIndexOf(value);
    if (index < 0) {
      var actualIndex = -(index + 1);
      return (UInt64)actualIndex;
    }
    return (UInt64)index;
  }
}
