using Apache.Arrow;
using Apache.Arrow.Flight;
using Grpc.Core;
using Io.Deephaven.Proto.Backplane.Grpc;
using Io.Deephaven.Proto.Backplane.Script.Grpc;
using System;

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
  /// Creates a new table from this table, but including only the specified columns
  /// </summary>
  /// <param name="columnSpecs">The columnSpecs to select</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle Select(params string[] columnSpecs) {
    return SelectOrUpdateHelper(columnSpecs, _manager.Server.TableStub.SelectAsync);
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
  /// Creates a new table from this table containing the first 'n' rows of this table.
  /// </summary>
  /// <param name="n">Number of rows</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle Head(Int64 n) {
    return HeadOrTailHelper(n, true);
  }

  /// <summary>
  /// Creates a new table from this table containing the last 'n' rows of this table.
  /// </summary>
  /// <param name="n">Number of rows</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle Tail(Int64 n) {
    return HeadOrTailHelper(n, false);
  }

  private TableHandle HeadOrTailHelper(Int64 n, bool head) {
    var server = _manager.Server;
    var req = new HeadOrTailRequest {
      ResultId = server.NewTicket(),
      SourceId = new TableReference {
        Ticket = _ticket
      },
      NumRows = n
    };

    var resp = server.SendRpc(opts => head ? server.TableStub.HeadAsync(req, opts) :
      server.TableStub.TailAsync(req, opts));
    return TableHandle.Create(_manager, resp);
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::MinBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::MIN, std::move(column_specs));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::MaxBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::MAX, std::move(column_specs));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::SumBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::SUM, std::move(column_specs));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::AbsSumBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::ABS_SUM, std::move(column_specs));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::VarBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::VAR, std::move(column_specs));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::StdBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::STD, std::move(column_specs));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::AvgBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::AVG, std::move(column_specs));
  

  public TableHandle LastBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.LAST, columnSpecs);
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::FirstBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::FIRST, std::move(column_specs));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::MedianBy(std::vector<std::string> column_specs) {
    return DefaultAggregateByType(ComboAggregateRequest::MEDIAN, std::move(column_specs));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::DefaultAggregateByDescriptor(
    ComboAggregateRequest::Aggregate descriptor, std::vector<std::string> group_by_columns) {
    auto descriptors = MakeReservedVector<ComboAggregateRequest::Aggregate>(1);
    descriptors.push_back(std::move(descriptor));
    return By(std::move(descriptors), std::move(group_by_columns));
  }


  std::shared_ptr<TableHandleImpl> TableHandleImpl::DefaultAggregateByType(
    ComboAggregateRequest::AggType aggregate_type, std::vector<std::string> group_by_columns) {
    ComboAggregateRequest::Aggregate descriptor;
    descriptor.set_type(aggregate_type);
    return DefaultAggregateByDescriptor(std::move(descriptor), std::move(group_by_columns));
  }

  std::shared_ptr<TableHandleImpl> TableHandleImpl::By(
    std::vector<ComboAggregateRequest::Aggregate> descriptors,
    std::vector<std::string> group_by_columns) {
    auto* server = managerImpl_->Server().get();
    auto result_ticket = server->NewTicket();
    ComboAggregateRequest req;
    *req.mutable_result_id() = std::move(result_ticket);
    *req.mutable_source_id()->mutable_ticket() = ticket_;
    for (auto & agg : descriptors) {
      *req.mutable_aggregates()->Add() = std::move(agg);
    }
    for (auto & gbc : group_by_columns) {
      *req.mutable_group_by_columns()->Add() = std::move(gbc);
    }
    req.set_force_combo(false);
    ExportedTableCreationResponse resp;
    server->SendRpc([&](grpc::ClientContext * ctx) {
      return server->TableStub()->ComboAggregate(ctx, req, &resp);
    });
    return TableHandleImpl::Create(managerImpl_, std::move(resp));
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
