using Apache.Arrow;
using Apache.Arrow.Flight;
using Grpc.Core;
using Io.Deephaven.Proto.Backplane.Grpc;
using Io.Deephaven.Proto.Backplane.Script.Grpc;

namespace Deephaven.Dh_NetClient;

public class TableHandle : IDisposable {
  public static TableHandle Create(TableHandleManager manager,
    ExportedTableCreationResponse resp) {

    var server = manager.Server;
    var metadata = new Metadata();
    server.ForEachHeaderNameAndValue(metadata.Add);

    var fd = ArrowUtil.ConvertTicketToFlightDescriptor(resp.ResultId.Ticket);
    var schema = server.FlightClient.GetSchema(fd, metadata).ResponseAsync.Result;
    return new TableHandle(manager, resp.ResultId.Ticket, schema, resp.Size, resp.IsStatic);
  }

  private readonly TableHandleManager _manager;
  private readonly Ticket _ticket;
  private readonly Schema _schema;
  private readonly Int64 _numRows;
  private readonly bool _isStatic;

  private TableHandle(TableHandleManager manager, Ticket ticket, Schema schema, long numRows, bool isStatic) {
    _manager = manager;
    _ticket = ticket;
    _schema = schema;
    _numRows = numRows;
    _isStatic = isStatic;
  }

  public void Dispose() {
    Console.Error.WriteLine("TableHandle.Dispose: NIY");
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

  /// <summary>
  /// Creates a new table from this table, filtered by condition. Consult the Deephaven
  /// documentation for more information about valid conditions.
  /// </summary>
  /// <param name="condition">A Deephaven boolean expression such as "Price > 100" or "Col3 == Col1 * Col2"</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle Where(string condition) {
    var server = _manager.Server;
    var req = new UnstructuredFilterTableRequest {
      ResultId = server.NewTicket(),
      SourceId = new TableReference { Ticket = _ticket }
    };
    req.Filters.Add(condition);
    var resp = server.SendRpc(opts => server.TableStub.UnstructuredFilterAsync(req, opts));
    return TableHandle.Create(_manager, resp);
  }

  public void BindToVariable(string variable) {
    var server = _manager.Server;
    if (_manager.ConsoleId == null) {
      throw new Exception("Client was created without specifying a script language");
    }
    var req = new BindTableToVariableRequest {
      ConsoleId = _manager.ConsoleId,
      VariableName = variable,
      TableId = _ticket
    };

    _ = server.SendRpc(opts => server.ConsoleStub.BindTableToVariableAsync(req, opts));
  }

  public FlightRecordBatchStreamReader GetFlightStream() {
    var server = _manager.Server;
    var metadata = new Metadata();
    server.ForEachHeaderNameAndValue(metadata.Add);
    var ticket = new FlightTicket(_ticket.Ticket_);
    var call = _manager.Server.FlightClient.GetStream(ticket, metadata);
    return call.ResponseStream;
  }

  public Table ToArrowTable() {
    using var reader = GetFlightStream();
    // Gather record batches
    var recordBatches = new List<RecordBatch>();
    while (reader.MoveNext().Result) {
      recordBatches.Add(reader.Current);
    }

    var schema = reader.Schema.Result;
    var table = Table.TableFromRecordBatches(schema, recordBatches);
    return table;
  }

  public IClientTable ToClientTable() {
    var at = ToArrowTable();
    return ArrowClientTable.Create(at);
  }

  public IDisposable Subscribe(IObserver<TickingUpdate> observer) {
    var disposer = SubscriptionThread.Start(_manager.Server, _schema, _ticket, observer);
    // _manager.AddSubscriptionDisposer(disposer);
    return disposer;
  }

  public string ToString(bool wantHeaders) {
    var t = ToArrowTable();
    return t.ToString();
  }
}
