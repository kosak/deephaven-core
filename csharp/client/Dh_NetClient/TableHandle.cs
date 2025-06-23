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
  public readonly Schema Schema;
  private readonly Int64 _numRows;
  private readonly bool _isStatic;

  private TableHandle(TableHandleManager manager, Ticket ticket, Schema schema, long numRows, bool isStatic) {
    _manager = manager;
    _ticket = ticket;
    Schema = schema;
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
  /// View columnSpecs from a table. The columnSpecs can be column names or formulas like
  /// "NewCol = A + 12". See the Deephaven documentation for the difference between Select() and
  /// View().
  /// </summary>
  /// <param name="columnSpecs">The columnSpecs to select</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle View(params string[] columnSpecs) {
    return SelectOrUpdateHelper(columnSpecs, _manager.Server.TableStub.ViewAsync);
  }

  /// <summary>
  /// Creates a new table from this table, but including the additional specified columns
  /// </summary>
  /// <param name="columnSpecs">The columnSpecs to add. For example, "X = A + 5", "Y = X * 2"</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle Update(params string[] columnSpecs) {
    return SelectOrUpdateHelper(columnSpecs, _manager.Server.TableStub.UpdateAsync);
  }

  /// <summary>
  /// Creates a new table containing a new cached formula column for each argument.
  /// </summary>
  /// <param name="columnSpecs">The columnSpecs to add. For exampe, {"X = A + 5", "Y = X * 2"}.</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle LazyUpdate(params string[] columnSpecs) {
    return SelectOrUpdateHelper(columnSpecs, _manager.Server.TableStub.LazyUpdateAsync);
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
    req.ColumnSpecs.AddRange(columnSpecs);
    var resp = server.SendRpc(opts => func(req, opts));
    return TableHandle.Create(_manager, resp);
  }

  /// <summary>
  /// Creates a new table containing all of the unique values for a set of key columns.
  /// When used on multiple columns, it looks for distinct sets of values in the selected columns.
  /// </summary>
  /// <param name="columnSpecs">The columnSpecs to select</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle SelectDistinct(params string[] columnSpecs) {
    var server = _manager.Server;
    var req = new SelectDistinctRequest {
      ResultId = server.NewTicket(),
      SourceId = new TableReference {
        Ticket = _ticket
      }
    };
    req.ColumnNames.AddRange(columnSpecs);
    var resp = server.SendRpc(opts => server.TableStub.SelectDistinctAsync(req, opts));
    return TableHandle.Create(_manager, resp);
  }

  /// <summary>
  /// Creates a new table from this table Where the specified columns have been excluded.
  /// </summary>
  /// <param name="columnSpecs">The columnSpecs to exclude.</param>
  /// <returns></returns>
  public TableHandle DropColumns(params string[] columnSpecs) {
    var server = _manager.Server;
    var req = new DropColumnsRequest {
      ResultId = server.NewTicket(),
      SourceId = new TableReference {
        Ticket = _ticket
      }
    };
    req.ColumnNames.Add(columnSpecs);

    var resp = server.SendRpc(opts => server.TableStub.DropColumnsAsync(req, opts));
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

  public TableHandle MinBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.Min, columnSpecs);
  }

  public TableHandle MaxBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.Max, columnSpecs);
  }

  public TableHandle SumBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.Sum, columnSpecs);
  }

  public TableHandle AbsSumBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.AbsSum, columnSpecs);
  }

  public TableHandle VarBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.Var, columnSpecs);
  }

  public TableHandle StdBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.Std, columnSpecs);
  }

  public TableHandle AvgBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.Avg, columnSpecs);
  }

  public TableHandle LastBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.Last, columnSpecs);
  }

  public TableHandle FirstBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.First, columnSpecs);
  }

  public TableHandle MedianBy(params string[] columnSpecs) {
    return DefaultAggregateByType(ComboAggregateRequest.Types.AggType.Median, columnSpecs);
  }

  private TableHandle DefaultAggregateByDescriptor(ComboAggregateRequest.Types.Aggregate descriptor,
    IList<string> groupByColumns) {
    var descriptors = new[] {descriptor};
    return By(descriptors, groupByColumns);
  }

  private TableHandle DefaultAggregateByType(ComboAggregateRequest.Types.AggType aggregateType,
    IList<string> groupByColumns) {
    var descriptor = new ComboAggregateRequest.Types.Aggregate {
      Type = aggregateType
    };
    return DefaultAggregateByDescriptor(descriptor, groupByColumns);
  }

  public TableHandle By(IList<ComboAggregateRequest.Types.Aggregate> descriptors,
    IList<string> groupByColumns) {
    var server = _manager.Server;

    var req = new ComboAggregateRequest {
      ResultId = server.NewTicket(),
      SourceId = new TableReference { Ticket = _ticket },
      ForceCombo = false
    };
    req.Aggregates.AddRange(descriptors);
    req.GroupByColumns.AddRange(groupByColumns);

    var resp = server.SendRpc(opts => server.TableStub.ComboAggregateAsync(req, opts));
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

  /// <summary>
  /// Creates a new table containing rows from the source table, where the rows match values in the
  /// filter table.The filter is updated whenever either table changes.See the Deephaven
  /// documentation for the difference between "Where" and "WhereIn".
  /// </summary>
  /// <param name="filterTable">The table containing the set of values to filter on</param>
  /// <param name="columns">The columns to match on</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle WhereIn(TableHandle filterTable, params string[] columns) {
    var server = _manager.Server;
    var req = new WhereInRequest {
      ResultId = server.NewTicket(),
      LeftId = new TableReference { Ticket = _ticket },
      RightId = new TableReference { Ticket = filterTable._ticket },
      Inverted = false
    };
    req.ColumnsToMatch.AddRange(columns);
    var resp = server.SendRpc(opts => server.TableStub.WhereInAsync(req, opts));
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
    var disposer = SubscriptionThread.Start(_manager.Server, Schema, _ticket, observer);
    // _manager.AddSubscriptionDisposer(disposer);
    return disposer;
  }

  public string ToString(bool wantHeaders) {
    var t = ToArrowTable();
    return t.ToString();
  }
}
