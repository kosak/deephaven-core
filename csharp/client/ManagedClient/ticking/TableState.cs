namespace Deephaven.ManagedClient;

public class TableState {
  /// <summary>
  /// When the caller wants to add data to the ImmerTableState, they do it in two steps:
  /// AddKeys and then AddData.First, they call AddKeys, which updates the (key space) ->
  /// (position space) mapping. Immediately after this call (but before AddData), the key mapping
  /// will be inconsistent with respect to the data.But then, the caller calls AddData(perhaps
  /// all at once, or perhaps in slices) to fill in the data.Once the caller is done, the keys
  /// and data will be consistent once again.Note that AddKeys/AddData only support adding new keys.
  /// It is an error to try to re-add any existing key.
  /// </summary>
  /// <param name="rowsToAddKeySpace">Keys to add, represented in key space</param>
  /// <returns>Added keys, represented in index space</returns>
  /// <exception cref="NotImplementedException"></exception>
  public RowSequence AddKeys(RowSequence rowsToAddKeySpace) {
    throw new NotImplementedException("hi");
  }

  /// <summary>
  /// For each column i, insert the interval of data [begins[i], ends[i]) taken from the column source
  /// sources[i], into the table at the index space positions indicated by 'rowsToAddIndexSpace'.
  /// </summary>
  /// <param name="sources">The ColumnSources</param>
  /// <param name="begins">The array of start indices (inclusive) for each column</param>
  /// <param name="ends">The array of end indices (exclusive) for each columns</param>
  /// <param name="rowsToAddIndexSpace">Index space positions where the data should be inserted</param>
  public void AddData(IColumnSource[] sources, int[] begins, int[] ends, RowSequence rowsToAddIndexSpace) {
    throw new NotImplementedException("hi");
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


  void ApplyShifts(const RowSequence &start_index, const RowSequence &end_index,
  const RowSequence &dest_index);

  [[nodiscard]]
  std::shared_ptr<ClientTable> Snapshot() const;


}
