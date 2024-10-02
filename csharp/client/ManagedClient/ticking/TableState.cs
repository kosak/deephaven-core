using Apache.Arrow;
using System;

namespace Deephaven.ManagedClient;

public class TableState {
  private readonly SpaceMapper _spaceMapper = new();
  private readonly ArrayColumnSource[] _sourceData;
  private readonly int[] _sourceSizes;


  public TableState(Schema schema) {
    _sourceData = schema.FieldsList.Select(f => ArrayColumnSource.CreateFromArrowType(f.DataType, 0)).ToArray();
    _sourceSizes = new int[_sourceData.Length];
  }

  /// <summary>
  /// Adds the keys (represented in key space) to the SpaceMapper. Note that this method
  /// temporarily breaks the consistency between the key mapping and the data.
  /// To restore the consistency, the caller is expected to next call AddData.
  /// Note that AddKeys/AddData only support adding new keys.
  /// It is an error to try to re-add any existing key.
  /// </summary>
  /// <param name="keysToAddKeySpace">Keys to add, represented in key space</param>
  /// <returns>Added keys, represented in index space</returns>
  public RowSequence AddKeys(RowSequence keysToAddKeySpace) {
    return _spaceMapper.AddKeys(keysToAddKeySpace);
  }

  /// <summary>
  /// For each i, insert the interval of data srcRanges[i] taken from the column source
  /// sources[i], into the table at the index space positions indicated by 'rowsToAddIndexSpace'.
  /// Note that the values in 'rowsToAddIndexSpace' assume that the values are being inserted as the
  /// RowSequence is processed left to right. That is, a given index key in 'rowsToAddIndexSpace'
  /// is meant to be interpreted as though all the index keys to its left have already been added.
  ///
  /// <example>
  /// Assuming the original data has values [C,D,G,H]. This is densely packed (as always) into the ColumnSource at positions [0,1,2,3].
  /// And assume that the sources has [A,B,E,F] at requested positions [0, 1, 4, 5].
  /// The algorithm would run as follows:
  /// Next position is 0, and dest length is 0, so pull in new data A, so dest column is now [A]
  /// Next position is 1, and dest length is 1, so pull in new data B, so dest column is now [A,B]
  /// Next position is 4, and dest length is 2, so pull in *old* data C, so dest column is now [A,B,C] (and don't advance)
  /// Next position is still 4, and dest length is 3, so pull in *old* data D, so dest column is now [A,B,C,D] (and don't advance)
  /// Next position is still 4, and dest length is 4, so pull in new data E, so dest column is now [A,B,C,D,E]
  /// Next position is 5, and dest length is 5, so pull in new data F, so dest column is now [A,B,C,D,E,F]
  /// Leftover values are [G,H] so pull them in, and dest column is now [A,B,C,D,E,F,G,H]
  /// </example>
  /// </summary>
  /// <param name="sources">The ColumnSources</param>
  /// <param name="srcRanges">The data range to use for each column</param>
  /// <param name="rowsToAddIndexSpace">Index space positions where the data should be inserted</param>
  public void AddData(IColumnSource[] sources, Interval<int>[] srcRanges, RowSequence rowsToAddIndexSpace) {
    var ncols = sources.Length;
    var nrows = rowsToAddIndexSpace.Size.ToIntExact();
    Checks.AssertAllSame(_sourceData.Length, sources.Length, srcRanges.Length);

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
      var destData = origData.CreateOfSameType(destSize);
      var destDataIndex = 0;
      
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
      _sourceSizes[i] = destSize;
    }
  }

  private static void CopyChunk(IColumnSource src, int srcIndex, IMutableColumnSource dest, int destIndex, int numItems) {
    var chunk = Chunk.CreateChunkFor(src, numItems);
    var nulls = BooleanChunk.Create(numItems);
    var srcRs = RowSequence.CreateSequential((UInt64)srcIndex, (UInt64)srcIndex + (UInt64)numItems);
    src.FillChunk(srcRs, chunk, nulls);
    var destRs = RowSequence.CreateSequential((UInt64)destIndex, (UInt64)destIndex + (UInt64)numItems);
    dest.FillFromChunk(destRs, chunk, nulls);
  }

  /// <summary>
  /// Erases the data at the positions in 'rowsToEraseKeySpace'.
  /// </summary>
  /// <param name="rowsToEraseKeySpace">The keys, represented in key space, to erase</param>
  /// <returns>The keys, represented in index space, that were erased</returns>
  public RowSequence Erase(RowSequence rowsToEraseKeySpace) {
    var result = _spaceMapper.ConvertKeysToIndices(rowsToEraseKeySpace);
    var ncols = _sourceData.Length;
    var nrows = rowsToEraseKeySpace.Size;
    for (var i = 0; i != ncols; ++i) {
      var srcData = _sourceData[i];
      var srcIndex = 0;

      var destSize = ((UInt64)_sourceSizes[i] - (UInt64)nrows).ToIntExact();
      var destData = srcData.CreateOfSameType(destSize);
      var destDataIndex = 0;

      foreach (var (beginKey, endKey) in rowsToEraseKeySpace.Intervals) {
        var size = (endKey - beginKey).ToIntExact();
        var beginIndex = _spaceMapper.EraseRange(beginKey, endKey).ToIntExact();
        var endIndex = beginIndex + size;

        var numItemsToCopy = beginIndex - srcIndex;

        CopyChunk(srcData, srcIndex, destData, destDataIndex, numItemsToCopy);

        srcIndex = endIndex;
      }

      var numFinalItemsToCopy = _sourceSizes[i] - srcIndex;
      CopyChunk(srcData, srcIndex, destData, destDataIndex, numFinalItemsToCopy);

      _sourceData[i] = destData;
      _sourceSizes[i] = destSize;
    }
    return result;
  }

  /// <summary>
  /// Converts a RowSequence of keys represented in key space to a RowSequence of keys represented in index space.
  /// </summary>
  /// <param name="keysRowSpace">Keys represented in key space</param>
  /// <returns>Keys represented in index space</returns>
  public RowSequence ConvertKeysToIndices(RowSequence keysRowSpace) {
    return _spaceMapper.ConvertKeysToIndices(keysRowSpace);
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
