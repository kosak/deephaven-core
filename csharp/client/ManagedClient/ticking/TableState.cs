using System;
using Apache.Arrow;
using Array = System.Array;

namespace Deephaven.ManagedClient;

public class TableState {
  private readonly SpaceMapper _spaceMapper = new();
  private readonly IColumnSource[] _sourceData;
  private readonly int[] _sourceSizes;


  public TableState(Schema schema) {
    _dataSources = schema.FieldsList.Select(f => MakeEmptyArrayOfCorrespondingType(f.DataType)).ToArray();
  }

  /// <summary>
  /// When the caller wants to add data to the ImmerTableState, they do it in two steps:
  /// AddKeys and then AddData.First, they call AddKeys, which updates the (key space) ->
  /// (position space) mapping. Immediately after this call (but before AddData), the key mapping
  /// will be inconsistent with respect to the data.But then, the caller calls AddData(perhaps
  /// all at once, or perhaps in slices) to fill in the data. Once the caller is done, the keys
  /// and data will be consistent once again. Note that AddKeys/AddData only support adding new keys.
  /// It is an error to try to re-add any existing key.
  /// </summary>
  /// <param name="rowsToAddKeySpace">Keys to add, represented in key space</param>
  /// <returns>Added keys, represented in index space</returns>
  /// <exception cref="NotImplementedException"></exception>
  public RowSequence AddKeys(RowSequence rowsToAddKeySpace) {
    return _spaceMapper.AddKeys(rowsToAddKeySpace);
  }

  /// <summary>
  /// For each column i, insert the interval of data [begins[i], ends[i]) taken from the column source
  /// sources[i], into the table at the index space positions indicated by 'rowsToAddIndexSpace'.
  /// Note that the values 'rowsToAddIndexSpace' assume that the values are being inserted as the
  /// RowSequence is process. That is, assuming the original data has values [C,D,G,H] at positions [0,1,2,3].
  /// And assume that the sources has [A,B,E,F] at requested positions [0, 1, 4, 5].
  ///
  /// <example>
  /// This would effectively be interpreted as:
  /// Next position is 0, and dest length is 0, so pull in new data A, so column is now [A]
  /// Next position is 1, and dest length is 1, so pull in new data B, so column is now [A,B]
  /// Next position is 4, and dest length is 2, so pull in old data C, so column is now [A,B,C] (and don't advance)
  /// Next position is still 4, and dest length is 3, so pull in old data D, so column is now [A,B,C,D]
  /// Next position is still 4, and dest length is 4, so pull in new data E, so column is now [A,B,C,D,E]
  /// Next position is 5, and dest length is 5, so pull in new data F, so column is now [A,B,C,D,E,F]
  /// Leftover values are [G,H] so column is now [A,B,C,D,E,F,G,H]
  /// </example>
  /// </summary>
  /// <param name="sources">The ColumnSources</param>
  /// <param name="begins">The array of start indices (inclusive) for each column</param>
  /// <param name="ends">The array of end indices (exclusive) for each columns</param>
  /// <param name="rowsToAddIndexSpace">Index space positions where the data should be inserted</param>
  public void AddData(IColumnSource[] sources, int[] begins, int[] ends, RowSequence rowsToAddIndexSpace) {
    var ncols = sources.Length;
    var nrows = rowsToAddIndexSpace.Size.ToIntExact();
    Checks.AssertAllSame(sources.Length, begins.Length, ends.Length);
    if (ncols != _sourceData.Length) {
      throw new Exception($"Expected {_sourceData.Length} columns provided, got {ncols}");
    }

    for (var i = 0; i != ncols; ++i) {
      var numElementsProvided = ends[i] - begins[i];
      if (nrows > numElementsProvided) {
        throw new Exception(
          $"RowSequence demands {nrows} elements but column {i} has only been provided {numElementsProvided} elements");
      }

      var origData = _sourceData[i];
      var origDataIndex = 0;

      var newData = sources[i];
      var newDataIndex = 0;

      var destSize = _sourceSizes[i] + nrows;
      var destData = Array.CreateInstance(origData.GetType().GetElementType()!, destSize);
      int destDataIndex = 0;
      
      foreach (var interval in rowsToAddIndexSpace.Intervals) {
        var beginKey = interval.Item1.ToIntExact();
        var endKey = interval.Item2.ToIntExact();
        if (destDataIndex < beginKey) {
          var numItemsToTakeFromOrig = beginKey - destDataIndex;
          CopyChunk(origData, origDataIndex, destData, destDataIndex, numItemsToTakeFromOrig);
          origDataIndex += numItemsToTakeFromOrig;
          destDataIndex += numItemsToTakeFromOrig;  // aka destDataIndex = beginKey
        }

        var numItemsToTakeFromNew = endKey - beginKey;
        CopyChunk(newData, newDataIndex, destData, destDataIndex, numItemsToTakeFromNew);
        newDataIndex += numItemsToTakeFromNew;
        destDataIndex += numItemsToTakeFromNew;
      }

      var numFinalItemsToCopy = _sourceSizes[i] - origDataIndex;
      CopyChunk(origData, origDataIndex, destData, destDataIndex, numFinalItemsToCopy);

      _sourceData[i] = destData;
    }
  }

  /// <summary>
  /// Erases the data at the positions in 'rowsToEraseKeySpace'.
  /// </summary>
  /// <param name="rowsToEraseKeySpace">The keys, represented in key space, to erase</param>
  /// <returns>The keys, represented in index space, that were erased</returns>
  public RowSequence Erase(RowSequence rowsToEraseKeySpace) {
    throw new NotImplementedException("hi");
  }

  /// <summary>
  /// Converts a RowSequence of keys represented in key space to a RowSequence of keys represented in index space.
  /// </summary>
  /// <param name="keysRowSpace">Keys represented in key space</param>
  /// <returns>Keys represented in index space</returns>
  public RowSequence ConvertKeysToIndices(RowSequence keysRowSpace) {
    throw new NotImplementedException("hi");
  }

  /// <summary>
  /// Modifies column 'col_num' with the contiguous data sourced in 'src'
  /// at the half-open interval[begin, end), to be stored in the destination
  /// at the positions indicated by 'rows_to_modify_index_space'.
  /// </summary>
  /// <param name="colNum">Index of the column to be modified</param>
  /// <param name="src">A ColumnSource containing the source data</param>
  /// <param name="begin">The start of the source range</param>
  /// <param name="end">One past the end of the source range</param>
  /// <param name="rowsToModifyIndexSpace">The positions to be modified in the destination, represented in index space</param>
  public void ModifyData(int colNum, IColumnSource src, int begin, int end, RowSequence rowsToModifyIndexSpace) {
    throw new NotImplementedException("hi");
  }

  /// <summary>
  /// Applies shifts to the keys in key space. This does not affect the ordering of the keys,
  /// nor will it cause keys to overlap with other keys.Logically there is a set of tuples
  /// (firstKey, lastKey, destKey) which is to be interpreted as take all the existing keys
  /// in the *closed* range [firstKey, lastKey] and move them to the range starting at destKey.
  /// These tuples have been "transposed" into three different RowSequence data structures
  /// for possible better compression.
  /// </summary>
  /// <param name="firstIndex">The RowSequence containing the firstKeys</param>
  /// <param name="lastIndex">The RowSequence containing the lastKeys</param>
  /// <param name="destIndex">The RowSequence containing the destKeys</param>
  public void ApplyShifts(RowSequence firstIndex, RowSequence lastIndex, RowSequence destIndex) {
    Console.Error.WriteLine("TODO - Apply Shifts");
  }

  /// <summary>
  /// Takes a snapshot of the current table state
  /// </summary>
  /// <returns>A ClientTable representing the current table state</returns>
  /// <exception cref="NotImplementedException"></exception>
  public ClientTable Snapshot() {
    return null;
  }
}
