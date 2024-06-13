using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class DeephavenStateManager {
  public static readonly DeephavenStateManager Instance = new DeephavenStateManager();

  private object _sync = new object();
  private readonly HashSet<DeephavenHandler> _handlers = new();

  public void Connect() {

  }

  public void Reconnect() {

  }

  public IExcelObservable SnapshotTable(string tableName, TableFilter filter) {
    Debug.WriteLine("making another one why");
    var cp = new MyClientProvider();
    var handler = new SnapshotHandler(cp, tableName, filter);
    AddHandler(handler);
    return handler;
  }

  // public IExcelObservable SubscribeToTable(string tableName, TableFilter filter) {
  //   var handler = new SubscribeHandler(this, tableName, filter);
  //   AddHandler(handler);
  //   return handler;
  // }

  private void AddHandler(DeephavenHandler handler) {
    lock (_sync) {
      _handlers.Add(handler);
    }
  }
}

class MyClientProvider : IClientProvider {
  public bool TryGetClient([MaybeNullWhen(false)] out Client client) {
    client = null;
    return false;
  }
}
