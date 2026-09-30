//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
using Apache.Arrow;

namespace Deephaven.Dh_NetClient;

public record struct SourceAndRange(IColumnSource Source, Interval Range) {
}

public class TableState {
  private readonly Schema _schema;
  private readonly ArrayColumnSource[] _colData;
  private int _numRows;
  private readonly SpaceMapper _spaceMapper = new();

  /// <summary>
  /// The highest row count at which <see cref="Snapshot"/> handed out a reference to the current column
  /// storage, or 0 if it never has since the storage was last replaced.
  ///
  /// Snapshots share storage rather than copying it. That is safe precisely to the extent that later writes
  /// stay at or above this mark: a snapshot taken at N rows only ever reads [0, N), so appending at N and
  /// beyond cannot be observed by it, and growing the backing array leaves the snapshot holding the old one
  /// with [0, N) intact. A write below the mark would be visible, so those paths either build fresh storage
  /// or clone first.
  /// </summary>
  private int _sharedUpTo;


  public TableState(Schema schema) {
    _schema = schema;
    _colData = schema.FieldsList.Select(f => ArrayColumnSource.CreateFromArrowType(f.DataType, 0)).ToArray();
    _numRows = 0;
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
  /// Adds whichever of the given keys are absent and returns the positions of all of them. See
  /// SpaceMapper.AddKeysAllowingExisting.
  /// </summary>
  public RowSequence AddKeysAllowingExisting(RowSequence keysToAddKeySpace) {
    return _spaceMapper.AddKeysAllowingExisting(keysToAddKeySpace);
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
  /// <param name="sourcesAndRanges">The ColumnSources, with the data range to use for each column</param>
  /// <param name="rowsToAddIndexSpace">Index space positions where the data should be inserted</param>
  public void AddData(SourceAndRange[] sourcesAndRanges, RowSequence rowsToAddIndexSpace) {
    var ncols = sourcesAndRanges.Length;
    var nrows = rowsToAddIndexSpace.Count.ToIntExact();
    var newNumRows = _numRows + nrows;


    // An append - every new row lands at or past the current end - is the overwhelmingly common case while a
    // snapshot fills, and it can be done in place. The general path below has to build a fresh column and
    // interleave old and new data, which costs a copy of the whole table per batch and so makes filling a
    // table quadratic in its size. Appending instead grows the column geometrically and writes only the new
    // rows, which is what keeps a large snapshot linear.
    var isAppend = true;
    foreach (var interval in rowsToAddIndexSpace.Intervals) {
      if (interval.Begin.ToIntExact() < _numRows) {
        isAppend = false;
        break;
      }
    }

    for (var i = 0; i != ncols; ++i) {
      var sourceAndRange = sourcesAndRanges[i];
      var numElementsProvided = sourceAndRange.Range.Count.ToIntExact();
      if (nrows > numElementsProvided) {
        throw new Exception(
          $"RowSequence demands {nrows} elements but column {i} has only been provided {numElementsProvided} elements");
      }

      var origData = _colData[i];
      var origDataIndex = 0;

      var newData = sourceAndRange.Source;
      var newDataIndex = 0;

      if (isAppend) {
        // Appending only writes at or above _numRows, which is at or above any snapshot's row count, so a
        // snapshot sharing this storage cannot observe it. Growing the array leaves the snapshot on the old
        // one, with its own rows intact.
        origData.EnsureCapacity(newNumRows);
        foreach (var interval in rowsToAddIndexSpace.Intervals) {
          var beginKey = interval.Begin.ToIntExact();
          var count = interval.Count.ToIntExact();
          IColumnSource.Copy(newData, newDataIndex, origData, beginKey, count);
          newDataIndex += count;
        }
        continue;
      }

      var destData = origData.CreateOfSameType(newNumRows);
      var destDataIndex = 0;

      foreach (var interval in rowsToAddIndexSpace.Intervals) {
        var beginKey = interval.Begin.ToIntExact();
        var endKey = interval.End.ToIntExact();
        if (destDataIndex < beginKey) {
          var numItemsToTakeFromOrig = beginKey - destDataIndex;
          IColumnSource.Copy(origData, origDataIndex, destData, destDataIndex, numItemsToTakeFromOrig);
          origDataIndex += numItemsToTakeFromOrig;
          destDataIndex += numItemsToTakeFromOrig;  // aka destDataIndex = beginKey
        }

        var numItemsToTakeFromNew = endKey - beginKey;
        IColumnSource.Copy(newData, newDataIndex, destData, destDataIndex, numItemsToTakeFromNew);
        newDataIndex += numItemsToTakeFromNew;
        destDataIndex += numItemsToTakeFromNew;
      }

      var numFinalItemsToCopy = _numRows - origDataIndex;
      IColumnSource.Copy(origData, origDataIndex, destData, destDataIndex, numFinalItemsToCopy);

      _colData[i] = destData;
    }
    // Every column was rebuilt into storage no snapshot has seen, so nothing is shared any more.
    _sharedUpTo = 0;
    _numRows = newNumRows;
  }


  /// <summary>
  /// Erases the data at the positions in 'rowsToEraseKeySpace'.
  /// </summary>
  /// <param name="rowsToEraseKeySpace">The keys, represented in key space, to erase</param>
  /// <returns>The keys, represented in index space, that were erased</returns>
  public RowSequence Erase(RowSequence rowsToEraseKeySpace) {
    // Only the keys we actually hold can be erased. A full subscription is sent the server's whole removed
    // set, which during a growing snapshot can name rows this client has not been given yet; those are
    // tracked by the server's row set but absent from ours, and must be skipped rather than rejected.
    var result = _spaceMapper.ConvertPresentKeysToIndices(rowsToEraseKeySpace);
    var ncols = _colData.Length;
    var nrows = result.Count;
    var newNumRows = ((UInt64)_numRows - nrows).ToIntExact();

    for (var i = 0; i != ncols; ++i) {
      var srcData = _colData[i];
      var srcDataIndex = 0;

      var destData = srcData.CreateOfSameType(newNumRows);
      var destDataIndex = 0;

      foreach (var interval in result.Intervals) {
        var iBegin = interval.Begin.ToIntExact();
        var iEnd = interval.End.ToIntExact();
        // srcDataIndex is "where we left off" in the source
        // iBegin is "the next starting point to delete"
        // The gap between these two is the amount of data to keep, i.e. to copy over
        var numItemsToCopy = iBegin - srcDataIndex;

        IColumnSource.Copy(srcData, srcDataIndex, destData, destDataIndex, numItemsToCopy);

        srcDataIndex = iEnd;
        destDataIndex += numItemsToCopy;
      }

      var numFinalItemsToCopy = _numRows - srcDataIndex;
      IColumnSource.Copy(srcData, srcDataIndex, destData, destDataIndex, numFinalItemsToCopy);
      destDataIndex += numFinalItemsToCopy;
      if (destDataIndex != newNumRows) {
        throw new Exception($"Short erase operation: expected {newNumRows}, have {destDataIndex}");
      }

      _colData[i] = destData;
    }
    // Erase compacts into freshly allocated columns, so the live storage is private again.
    _sharedUpTo = 0;

    foreach (var interval in rowsToEraseKeySpace.Intervals) {
      _spaceMapper.EraseRange(interval);
    }

    _numRows = newNumRows;
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
  /// <param name="sourceAndRange">The ColumnSource containing the source data, and the source range within it to modify</param>
  /// <param name="rowsToModifyIndexSpace">The positions to be modified in the destination, represented in index space</param>
  public void ModifyData(int colNum, SourceAndRange sourceAndRange, RowSequence rowsToModifyIndexSpace) {
    var nrows = rowsToModifyIndexSpace.Count;
    var sourceCount = sourceAndRange.Range.Count;
    if (nrows > sourceCount) {
      throw new Exception($"Insufficient data in source: have {sourceCount}, need {nrows}");
    }

    // Unlike adds, modifies overwrite existing positions, which an outstanding snapshot can see. Take private
    // storage first so the snapshot keeps the values it was created with.
    UnshareColumns();

    var srcCol = sourceAndRange.Source;
    var srcRemaining = sourceAndRange.Range;
    var destCol = _colData[colNum];
    foreach (var destInterval in rowsToModifyIndexSpace.Intervals) {
      var destCount = destInterval.Count.ToIntExact();
      var chunk = ChunkMaker.CreateChunkFor(srcCol, destCount);
      var nulls = BooleanChunk.Create(destCount);
      var (thisRange, residual) = srcRemaining.Split((UInt64)destCount);
      var srcRowSeq = RowSequence.CreateSequential(thisRange);
      var destRowSeq = RowSequence.CreateSequential(destInterval);
      srcCol.FillChunk(srcRowSeq, chunk, nulls);
      destCol.FillFromChunk(destRowSeq, chunk, nulls);
      srcRemaining = residual;
    }
  }

  /// <summary>
  /// Applies shifts to the keys in key space. This does not affect the ordering of the keys,
  /// nor will it cause keys to overlap with other keys. Logically there is a set of tuples
  /// (firstKey, lastKey, destKey) which is to be interpreted as take all the existing keys
  /// in the *closed* range [firstKey, lastKey] and move them to the range starting at destKey.
  /// These tuples have been "transposed" into three different RowSequence data structures
  /// for possible better compression.
  /// </summary>
  /// <param name="firstIndex">The RowSequence containing the firstKeys</param>
  /// <param name="lastIndex">The RowSequence containing the lastKeys. Note that these are in *closed* interval representation</param>
  /// <param name="destIndex">The RowSequence containing the destKeys</param>
  public void ApplyShifts(RowSequence firstIndex, RowSequence lastIndex, RowSequence destIndex) {
    foreach (var (range, destKey) in ShiftProcessor.AnalyzeShiftData(firstIndex, lastIndex, destIndex)) {
      _spaceMapper.ApplyShift(range, destKey);
    }
  }

  /// <summary>
  /// Takes a snapshot of the current table state.
  /// </summary>
  /// <remarks>
  /// This is on the per-message path, not a user-facing freeze: processing one Barrage message takes three or
  /// four snapshots to fill in the before/after tables of a <see cref="TickingUpdate"/>. Copying the whole
  /// table that many times per message is what made filling a large table quadratic, so the snapshot shares
  /// the live column storage instead and the mutating paths copy when they would disturb it.
  /// </remarks>
  /// <returns>A ClientTable representing the current table state</returns>
  public IClientTable Snapshot() {
    // Handing out the live storage obliges every subsequent write to either stay above _numRows or clone
    // first; see _sharedUpTo.
    if (_numRows > _sharedUpTo) {
      _sharedUpTo = _numRows;
    }
    return new TableStateClientTable(_schema, (ArrayColumnSource[])_colData.Clone(), _numRows);
  }

  /// <summary>
  /// Gives this state private column storage again, so that writes below <see cref="_sharedUpTo"/> cannot be
  /// seen by a snapshot that is still holding the old storage.
  /// </summary>
  private void UnshareColumns() {
    if (_sharedUpTo == 0) {
      return;
    }

    for (var i = 0; i != _colData.Length; ++i) {
      var src = _colData[i];
      var dest = src.CreateOfSameType(_numRows);
      IColumnSource.Copy(src, 0, dest, 0, _numRows);
      _colData[i] = dest;
    }

    _sharedUpTo = 0;
  }
}

sealed class TableStateClientTable : IClientTable {
  public Schema Schema { get; }
  private readonly ArrayColumnSource[] _colData;
  private readonly int _numRows;
  public RowSequence RowSequence { get; }

  public TableStateClientTable(Schema schema, ArrayColumnSource[] colData, int numRows) {
    Schema = schema;
    _colData = colData;
    _numRows = numRows;
    RowSequence = RowSequence.CreateSequential(Interval.OfStartAndSize(0, (UInt64)numRows));
  }

  public void Dispose() {
    // Nothing to do.
  }

  public IColumnSource GetColumn(int columnIndex) => _colData[columnIndex];

  public long NumRows => _numRows;
  public long NumCols => _colData.Length;

  public override string ToString() {
    return ((IClientTable)this).ToString(true, false);
  }
}
