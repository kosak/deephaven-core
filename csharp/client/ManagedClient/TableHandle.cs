using Apache.Arrow;
using Apache.Arrow.Flight;
using Grpc.Core;
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
    return SelectOrUpdateHelper(columnSpecs, _manager.Server.TableStub.UpdateAsync);
  }

  private TableHandle SelectOrUpdateHelper(string[] columnSpecs,
    Func<SelectOrUpdateRequest, CallOptions, AsyncUnaryCall<ExportedTableCreationResponse>> func) {
    var server = _manager.Server;
    var req = new SelectOrUpdateRequest {
      ResultId = server.NewTicket(),
      SourceId = new TableReference {
        Ticket = _ticket
      }
    };
    foreach (var cs in columnSpecs) {
      req.ColumnSpecs.Add(cs);
    }

    var resp = server.SendRpc(opts => func(req, opts));
    return TableHandle.Create(_manager, resp);
  }

  public string ToString(bool wantHeaders) {
    var server = _manager.Server;
    var metadata = new Metadata();
    server.ForEachHeaderNameAndValue(metadata.Add);
    var ticket = new FlightTicket(_ticket.Ticket_);
    using var stream = _manager.Server.FlightClient.GetStream(ticket, metadata);
    var reader = stream.ResponseStream;

    // Gather record batches
    var recordBatches = new List<RecordBatch>();
    while (reader.MoveNext().Result) {
      recordBatches.Add(reader.Current);
    }

    var schema = reader.Schema.Result;
    var table = Table.TableFromRecordBatches(schema, recordBatches);
    return table.ToString();
  }
}
