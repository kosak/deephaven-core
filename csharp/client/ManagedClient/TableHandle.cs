using Apache.Arrow;
using Apache.Arrow.Flight;
using Grpc.Core;
using Io.Deephaven.Proto.Backplane.Grpc;
using System.IO;
using System.Reflection.PortableExecutable;
using Apache.Arrow.Flight.Client;
using Google.FlatBuffers;
using Google.Protobuf;
using io.deephaven.barrage.flatbuf;
using Array = System.Array;
using Table = Apache.Arrow.Table;

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
    var temp = result.RequestStream.WriteAsync(uselessMessage, applicationMetadata);
    temp.Wait();

    while (true) {
      var mn = result.ResponseStream.MoveNext().Result;
      if (!mn) {
        Console.Error.WriteLine("all done");
        break;
      }

      var md = result.ResponseStream.ApplicationMetadata;
      var stupid = result.ResponseStream.Current;

      if (md.Count > 0) {
        var bytes = md[0].ToByteArray();
        var bb = new ByteBuffer(bytes);
        var bmw = BarrageMessageWrapper.GetRootAsBarrageMessageWrapper(bb);
        var rr = bmw.MsgType;

        Console.WriteLine("SHALL WE BEGIN");

        var bp = new BarrageProcessor();
        bp.ProcessNextChunk(null, null, bytes);
      }

      Console.WriteLine("hi");
    }
  }

  public string ToString(bool wantHeaders) {
    var t = ToArrowTable();
    return t.ToString();
  }
}
