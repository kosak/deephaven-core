namespace Deephaven.ManagedClient;

public class TableHandle : IDisposable {
  public void Dispose() {
    throw new NotImplementedException();
  }

  /// <summary>
  /// Creates a new table from this table, but including the additional specified columns
  /// </summary>
  /// <param name="columnSpecs">The columnSpecs to add. For example, "X = A + 5", "Y = X * 2"</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle Update(params string[] columnSpecs) {
    throw new NotImplementedException();
  }

  public string ToString(bool wantHeaders) {
    throw new NotImplementedException();
  }
}
