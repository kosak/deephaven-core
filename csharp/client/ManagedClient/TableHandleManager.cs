using System.Reflection.Metadata.Ecma335;
using System;

namespace Deephaven.ManagedClient;

/// <summary>
/// This class is the main way to get access to new TableHandle objects, via methods like EmptyTable()
/// and FetchTable(). A TableHandleManager is created by Client.GetManager(). You can have more than
/// one TableHandleManager for a given client. The reason you'd want more than one is that (in the
/// future) you will be able to set parameters here that will apply to all TableHandles created
/// by this class, such as flags that control asynchronous behavior.
/// </summary>
public class TableHandleManager : IDisposable {
  public static TableHandleManager Create(params object?[] ignored) {
    throw new NotImplementedException();
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  /// <summary>
  /// Creates a "zero-width" table on the server. Such a table knows its number of rows but has no columns.
  /// </summary>
  /// <param name="size">Number of rows in the empty table</param>
  /// <returns>The TableHandle of the new table</returns>
  public TableHandle EmptyTable(Int64 size) {
    throw new NotImplementedException();
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
    throw new NotImplementedException();
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
