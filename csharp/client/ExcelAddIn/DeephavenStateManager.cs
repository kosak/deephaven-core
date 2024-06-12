using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDna.Integration;

namespace Deephaven.Client.ExcelAddIn;

internal class DeephavenStateManager {
  public static readonly DeephavenStateManager Instance;

  private object _sync = new object();
  private int _nextKeyIndex = 0;
  private readonly HashSet<DeephavenHandler> _handlers = new();

  public void Connect() {

  }

  public void Reconnect() {

  }

  public string MakeUniqueKey() {
    lock (_sync) {
      return "uniqueKey" + _nextKeyIndex++;
    }
  }

  public IExcelObservable SnapshotTable(string tableName, TableFilter filter) {
    var handler = new SnapshotHandler(this, tableName, filter);
    AddHandler(handler);
    return handler;
  }

  public IExcelObservable SubscribeToTable(string tableName, TableFilter filter) {
    var handler = new SubscribeHandler(this, tableName, filter);
    AddHandler(handler);
    return handler;
  }

  private void AddHandler(IDeephavenHandler handler) {
    lock (_sync) {
      _handlers.Add(handler);
    }
  }
}
