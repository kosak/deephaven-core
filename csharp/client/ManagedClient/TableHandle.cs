using Apache.Arrow;
using Io.Deephaven.Proto.Backplane.Grpc;

namespace Deephaven.ManagedClient;

public class TableHandle : IDisposable {
  public static TableHandle Create(TableHandleManager manager,
    ExportedTableCreationResponse resp) {
    return new TableHandle(manager, resp.ResultId.Ticket, resp.Size, resp.IsStatic);
  }

  private readonly TableHandleManager _manager;
  private readonly Ticket _ticket;
  private readonly Int64 _numRows;
  private readonly bool _isStatic;

  private TableHandle(TableHandleManager manager, Ticket ticket, long numRows, bool isStatic) {
    _manager = manager;
    _ticket = ticket;
    _numRows = numRows;
    _isStatic = isStatic;
  }

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
