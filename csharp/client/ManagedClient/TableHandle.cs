using Apache.Arrow;
using Apache.Arrow.Flight;
using Grpc.Core;
using Io.Deephaven.Proto.Backplane.Grpc;
using Google.FlatBuffers;
using Google.Protobuf;
using io.deephaven.barrage.flatbuf;
using Table = Apache.Arrow.Table;

namespace Deephaven.ManagedClient;

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
    Console.Error.WriteLine("NIY");
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

  public ClientTable ToClientTable() {
    var at = ToArrowTable();
    return ArrowClientTable.Create(at);
  }

  public void ZamboniTime() {
    var server = _manager.Server;
    var metadata = new Metadata();
    server.ForEachHeaderNameAndValue(metadata.Add);
    var fc = server.FlightClient;
    // var command = "nhpd"u8.ToArray();
    var command = "dphn"u8.ToArray();
    var fd = FlightDescriptor.CreateCommandDescriptor(command);
    var result = fc.DoExchange(fd, metadata);

    var batchBuilder = new RecordBatch.Builder();
    var arrayBuilder = new Int32Array.Builder();
    batchBuilder.Append("test", true, arrayBuilder.Build());
    var uselessMessage = batchBuilder.Build();

    var magicBytes = new byte[] {
      16, 0, 0, 0, 0, 0, 10, 0, 16, 0, 8, 0, 7, 0, 12, 0, 10, 0, 0, 0, 0, 0, 0, 5, 100, 112, 104, 110, 4, 0, 0, 0, 68,
      0, 0, 0, 16, 0, 0, 0,
      12, 0, 12, 0, 4, 0, 0, 0, 0, 0, 8, 0, 12, 0, 0, 0, 8, 0, 0, 0, 32, 0, 0, 0, 5, 0, 0, 0, 101, 4, 0, 0, 0, 0, 0, 0,
      16, 0, 12, 0, 0, 0, 6, 0, 0, 0, 8, 0, 0, 0, 7, 0, 16, 0, 0, 0, 0, 0, 1, 1, 0, 16, 0, 0
    };

    const int magicOffset = 68;
    if (magicBytes[magicOffset] != 101) {
      throw new Exception("Programming error in magicBytes");
    }
    _ticket.Ticket_.Span.CopyTo(magicBytes.AsSpan(magicOffset));

    var applicationMetadata = ByteString.CopyFrom(magicBytes);
    result.RequestStream.WriteAsync(uselessMessage, applicationMetadata).Wait();

    var responseStream = result.ResponseStream;

    var numCols = _schema.FieldsList.Count;

    while (true) {
      var mn = responseStream.MoveNext().Result;
      if (!mn) {
        Console.Error.WriteLine("all done");
        break;
      }

      byte[]? metadateBytes = null;

      var mds = responseStream.ApplicationMetadata;
      if (mds.Count > 0) {
        if (mds.Count > 1) {
          throw new Exception($"Expected metadata count 1, got {mds.Count}");
        }

        metadateBytes = mds[0].ToByteArray();
      }

      var recordBatch = responseStream.Current;
      if (recordBatch.ColumnCount != numCols) {
        throw new Exception($"Expected {numCols} columns in RecordBatch, got {recordBatch.ColumnCount}");
      }

      var columns = new IColumnSource[numCols];
      var sizes = new int[numCols];
      for (int i = 0; i != numCols; ++i) {
        var rbCol = recordBatch.Column(i);
        if (rbCol is not ListArray la) {
          throw new Exception($"Expected ListArray type, got {rbCol.GetType().Name}");
        }

        var (cs, size) = ArrowUtil.MakeColumnSourceFromListArray(la);
        columns[i] = cs;
        sizes[i] = size;
      }
      
      Console.WriteLine("SHALL WE BEGIN");



      var bp = new BarrageProcessor(numCols);
      bp.ProcessNextChunk(columns, sizes, metadateBytes);

      Console.WriteLine("hi");
    }
  }

  public string ToString(bool wantHeaders) {
    var t = ToArrowTable();
    return t.ToString();
  }
}
