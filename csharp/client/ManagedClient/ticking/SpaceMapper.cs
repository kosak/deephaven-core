using C5;

namespace Deephaven.ManagedClient;

public class SpaceMapper {
  private readonly TreeSet<UInt64> _set = new();

  /// <summary>
  /// Adds the keys in the half-open interval [begin_key, end_key_) to the set.
  /// The keys must not already exist in the set.If they do, an exception is thrown.
  /// </summary>
  /// <param name="beginKey">The first key to insert</param>
  /// <param name="endKey">One past the last key to insert</param>
  /// <returns>The rank of beginKey</returns>
  UInt64 AddRange(UInt64 beginKey, UInt64 endKey) {
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

    return (UInt64)index;
  }

  /// <summary>
  /// Removes the keys in the half-open interval[begin_key, end_key_) from the set.
  /// It is ok if some or all of the keys do not exist in the set.
  /// </summary>
  /// <param name="beginKey">The first key to remove</param>
  /// <param name="endKey">One past the last key to remove</param>
  /// <returns>The rank of beginKey</returns>
  UInt64 EraseRange(UInt64 beginKey, UInt64 endKey) {
    var result = ZeroBasedRank(beginKey);
    _set.RemoveRangeFromTo(beginKey, endKey);
    return result;
  }

  void ApplyShift(UInt64 begin_key, UInt64 end_key, UInt64 dest_key) {
    throw new NotImplementedException("NIY");
  }

  /**
   * Adds 'keys' (specified in key space) to the map, and returns the positions (in position
   * space) of those keys after insertion. 'keys' are required to not already been in the map.
   * The algorithm behaves as though all the keys are inserted in the map and then
   * 'ConvertKeysToIndices' is called.
   *
   * Example:
   *   SpaceMapper currently holds [100 300]
   *   AddKeys called with [1, 2, 200, 201, 400, 401]
   *   SpaceMapper final state is [1, 2, 100, 200, 201, 300, 400, 401]
   *   The returned result is [0, 1, 3, 4, 6, 7]
   */
  RowSequence AddKeys(RowSequence begin_key) {
    throw new NotImplementedException("NIY");
  }

  /**
   * Looks up 'keys' (specified in key space) in the map, and returns the positions (in position
   * space) of those keys.
   */
  RowSequence ConvertKeysToIndices(RowSequence begin_key) {
    throw new NotImplementedException("NIY");
  }

  int Cardinality() {
    return _set.Count;
  }

  UInt64 ZeroBasedRank(UInt64 value) {
    var index = _set.LastIndexOf(value);
    if (index < 0) {
      var actualIndex = -(index + 1);
      return (UInt64)actualIndex;
    }
    return (UInt64)index;
  }
}
