﻿using System.Diagnostics.CodeAnalysis;
using Apache.Arrow;

namespace Deephaven.ManagedClient;

public abstract class ClientTable {
  /// <summary>
  /// Get the RowSequence (in position space) that underlies this Table.
  /// </summary>
  /// <returns>The RowSequence</returns>
  public abstract RowSequence RowSequence { get; }

  /// <summary>
  /// Gets a ColumnSource from the ClientTable by index
  /// </summary>
  /// <param name="columnIndex"></param>
  /// <returns>The ColumnSource</returns>
  public abstract IColumnSource GetColumn(int columnIndex);

  /// <summary>
  /// Gets a ColumnSource from the ClientTable by name.
  /// </summary>
  /// <param name="name">The name of the column</param>
  /// <returns>The ColumnSource, if 'name' was found. Otherwise, throws an exception.</returns>
  public IColumnSource GetColumn(string name) {
    _ = TryGetColumnInternal(name, true, out var result);
    return result!;
  }

  /// <summary>
  /// Gets a ColumnSource from the ClientTable by name.
  /// </summary>
  /// <param name="name">The name of the column</param>
  /// <param name="result">The column, if found</param>
  /// <returns>True if 'name' was found, false otherwise.</returns>
  public bool TryGetColumn(string name, [NotNullWhen(true)] out IColumnSource? result) {
    return TryGetColumnInternal(name, false, out result);
  }

  private bool TryGetColumnInternal(string name, bool strict, [NotNullWhen(true)] out IColumnSource? result) {
    if (TryGetColumnIndex(name, out var index)) {
      result = GetColumn(index);
      return true;
    }

    if (strict) {
      throw new Exception($"Column {name} was not found");
    }

    result = null;
    return false;
  }

  /// <summary>
  /// Gets the index of a ColumnSource from the ClientTable by name.
  /// </summary>
  /// <param name="name">The name of the column</param>
  /// <param name="result">The column index, if found</param>
  /// <returns>True if 'name' was found, false otherwise.</returns>
  public bool TryGetColumnIndex(string name, out int result) {
    result = Schema.GetFieldIndex(name);
    return result >= 0;
  }

  /// <summary>
  /// Number of rows in the ClienTTable
  /// </summary>
  public abstract Int64 NumRows { get; }

  /// <summary>
  /// Number of columns in the ClienTTable
  /// </summary>
  public abstract Int64 NumCols { get; }

  /// <summary>
  /// The ClientTable Schema
  /// </summary>
  public abstract Schema Schema { get; }
}