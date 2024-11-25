using Apache.Arrow.Flight;
using Apache.Arrow;
using Apache.Arrow.Flight.Client;
using Google.Protobuf;
using Grpc.Core;
using Io.Deephaven.Proto.Backplane.Grpc;
using System.Net.Sockets;

namespace Deephaven.ManagedClient;

internal class SubscriptionThread {
  public static IDisposable Start(Server server, Schema schema, Ticket ticket, ITickingCallback callback) {
    // std::promise<std::shared_ptr<SubscriptionHandle>> promise;
    // auto future = promise.get_future();
    // std::vector<int8_t> ticket_bytes(ticket.ticket().begin(), ticket.ticket().end());
    // auto ss = std::make_shared<SubscribeState>(std::move(server), std::move(ticket_bytes),
    //   std::move(schema), std::move(promise), std::move(callback));
    // flight_executor->Invoke([ss] () { ss->Invoke(); });
    // return future.get();

    var metadata = new Metadata();
    server.ForEachHeaderNameAndValue(metadata.Add);
    var fcw = server.FlightClient;
    var command = "dphn"u8.ToArray();
    var fd = FlightDescriptor.CreateCommandDescriptor(command);
    var exchange = fcw.DoExchange(fd, metadata);
    var result = UpdateProcessor.Start(exchange);
    return result;
  }

  public class UpdateProcessor : IDisposable {
    public UpdateProcessor Start() {

    }

    private readonly FlightRecordBatchExchangeCall _exchange;
    private readonly Schema _schema;
    private readonly Ticket _ticket;
    private readonly ITickingCallback _callback;

    public void RunForever() {

    }

    private void RunForeverHelper() {
      var batchBuilder = new RecordBatch.Builder();
      var arrayBuilder = new Int32Array.Builder();
      batchBuilder.Append("Dummy", true, arrayBuilder.Build());
      var uselessMessage = batchBuilder.Build();

      var subReq = BarrageProcessor.CreateSubscriptionRequest(_ticket.Ticket_.ToByteArray());
      var subReqAsByteString = ByteString.CopyFrom(subReq);
      _exchange.RequestStream.WriteAsync(uselessMessage, subReqAsByteString).Wait();

      var responseStream = _exchange.ResponseStream;

      var numCols = _schema.FieldsList.Count;
      var bp = new BarrageProcessor(_schema);

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
}