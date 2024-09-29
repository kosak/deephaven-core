using System.Reflection.Metadata.Ecma335;
using System;
using Io.Deephaven.Proto.Backplane.Grpc;

namespace Deephaven.ManagedClient;

/// <summary>
/// This class is the main way to get access to new TableHandle objects, via methods like EmptyTable()
/// and FetchTable(). A TableHandleManager is created by Client.GetManager(). You can have more than
/// one TableHandleManager for a given client. The reason you'd want more than one is that (in the
/// future) you will be able to set parameters here that will apply to all TableHandles created
/// by this class, such as flags that control asynchronous behavior.
/// </summary>
public class TableHandleManager : IDisposable {
  public static TableHandleManager Create(Ticket? consoleId, Server server, Executor executor,
    Executor flightExecutor) {
    return new TableHandleManager(consoleId, server, executor, flightExecutor);
  }

  private readonly Ticket? _consoleId;
  public readonly Server Server;
  private readonly Executor _executor;
  private readonly Executor _flightExecutor;

  private TableHandleManager(Ticket? consoleId, Server server, Executor executor,
    Executor flightExecutor) {
    _consoleId = consoleId;
    Server = server;
    _executor = executor;
    _flightExecutor = flightExecutor;
  }

  public void Dispose() {
    Console.Error.WriteLine("NIY");
  }

  /// <summary>
  /// Creates a "zero-width" table on the server. Such a table knows its number of rows but has no columns.
  /// </summary>
  /// <param name="size">Number of rows in the empty table</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle EmptyTable(Int64 size) {
    var req = new EmptyTableRequest {
      ResultId = Server.NewTicket(),
      Size = size
    };
    var resp = Server.SendRpc(opts => Server.TableStub.EmptyTableAsync(req, opts));
    return TableHandle.Create(this, resp);
  }

  /// <summary>
  /// Looks up an existing table by name.
  /// </summary>
  /// <param name="tableName">The name of the table</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle FetchTable(string tableName) {
    throw new NotImplementedException();
  }

  /// <summary>
  /// Creates a ticking table
  /// </summary>
  /// <param name="period">Table ticking frequency, specified as a TimeSpan,
  /// Int64 nanoseconds, or a string containing an ISO 8601 duration representation</param>
  /// <param name="startTime">When the table should start ticking, specified as a std::chrono::time_point,
  /// Int64 nanoseconds since the epoch, or a string containing an ISO 8601 time point specifier</param>
  /// <param name="blinkTable">Whether the table is a blink table</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle TimeTable(DurationSpecifier period, TimePointSpecifier? startTime = null,
    bool blinkTable = false) {
    var req = new TimeTableRequest {
      ResultId = Server.NewTicket(),
      BlinkTable = blinkTable
    };

    period.Visit(
      nanos => req.PeriodNanos = nanos,
      duration => req.PeriodString = duration);
    if (startTime.HasValue) {
      startTime.Value.Visit(
        nanos => req.StartTimeNanos = nanos,
        duration => req.StartTimeString = duration);
    }

    var resp = Server.SendRpc(opts => Server.TableStub.TimeTableAsync(req, opts));
    return TableHandle.Create(this, resp);
  }

  /// <summary>
  /// Creates an input table from an initial table. When key columns are provided, the InputTable
  /// will be keyed, otherwise it will be append-only.
  /// </summary>
  /// <param name="initialTable">The initial table</param>
  /// <param name="keyColumns">The set of key columns</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle InputTable(TableHandle initialTable, params string[] keyColumns) {
    throw new NotImplementedException();
  }

  /// <summary>
  /// Execute a script on the server. This assumes that the Client was created with a sessionType corresponding to
  /// the language of the script(typically either "python" or "groovy") and that the code matches that language
  /// </summary>
  /// <param name="code">The script to be run on the server</param>
  public void RunScript(string code) {
    throw new NotImplementedException();
  }
};
