using Apache.Arrow.Flight;
using Apache.Arrow;
using Apache.Arrow.Flight.Client;
using Google.Protobuf;
using Grpc.Core;
using Io.Deephaven.Proto.Backplane.Grpc;

namespace Deephaven.ManagedClient;

internal class SubscriptionThread {
}

public class SubscribeState {
  private readonly TableHandleManager _manager;
  private readonly Ticket _ticket;

  public void Annoying() {
    var server = _manager.Server;
    var metadata = new Metadata();
    server.ForEachHeaderNameAndValue(metadata.Add);
    var fc = server.FlightClient;
    var command = "dphn"u8.ToArray();
    var fd = FlightDescriptor.CreateCommandDescriptor(command);
    var result = fc.DoExchange(fd, metadata);

    var batchBuilder = new RecordBatch.Builder();
    var arrayBuilder = new Int32Array.Builder();
    batchBuilder.Append("Dummy", true, arrayBuilder.Build());
    var uselessMessage = batchBuilder.Build();

    var subReq = BarrageProcessor.CreateSubscriptionRequest(_ticket.Ticket_.ToByteArray());
    var subReqAsByteString = ByteString.CopyFrom(subReq);
    result.RequestStream.WriteAsync(uselessMessage, subReqAsByteString).Wait();

    var responseStream = result.ResponseStream;
  }
}

public class UpdateProcessor {
  private readonly Schema _schema;
  private readonly FlightClientRecordBatchStreamReader _responseStream;
  private readonly ITickingCallback _callback;

  public void ZamboniTime() {
    var numCols = _schema.FieldsList.Count;
    var bp = new BarrageProcessor(_schema);

    while (true) {
      var mn = _responseStream.MoveNext().Result;
      if (!mn) {
        Console.Error.WriteLine("all done");
        break;
      }

      byte[]? metadateBytes = null;

      var mds = _responseStream.ApplicationMetadata;
      if (mds.Count > 0) {
        if (mds.Count > 1) {
          throw new Exception($"Expected metadata count 1, got {mds.Count}");
        }

        metadateBytes = mds[0].ToByteArray();
      }

      var recordBatch = _responseStream.Current;
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

        var (cs, size) = ArrowColumnSource.CreateFromListArray(la);
        columns[i] = cs;
        sizes[i] = size;
      }

      Console.WriteLine("SHALL WE BEGIN");

      var tup = bp.ProcessNextChunk(columns, sizes, metadateBytes);
      if (tup != null) {
        _callback.OnTick(tup);
      }
    }
  }
}
