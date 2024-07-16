using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.ExcelAddIn;
using ExcelDna.Integration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

internal sealed class OperationManager {
  private readonly object _sync = new();
  private readonly Queue<Action<TableOperationManagerState>> _actions = new();

  public OperationManager() {
    new Thread(Doit) { IsBackground = true }.Start();
  }

  public void Register(IOperation operation) {
    Invoke(ts => {
      ts.TableOperations.Add(operation);
      operation.Start(ts.OperationMessage);
    });
  }

  public void Unregister(IOperation operation) {
    Invoke(ts => {
      ts.TableOperations.Remove(operation);
      operation.Stop();
    });
  }

  public void Connect(string connectionString) {
    Invoke(ts => ts.StartConnect(this, connectionString));
  }

  private void Invoke(Action<TableOperationManagerState> a) {
    lock (_sync) {
      _actions.Enqueue(a);
      Monitor.PulseAll(_sync);
    }
  }

  private void Doit() {
    var state = new TableOperationManagerState();
    while (true) {
      Action<TableOperationManagerState>? action;
      lock (_sync) {
        while (true) {
          if (_actions.TryDequeue(out action)) {
            break;
          }

          Monitor.Wait(_sync);
        }
      }

      action(state);
    }
  }

  private sealed class TableOperationManagerState {
    public OperationMessage OperationMessage = OperationMessage.Of("Not connected to Deephaven");
    public readonly HashSet<IOperation> TableOperations = new();
    private object _connectionCookie = new();

    public void StartConnect(OperationManager owner, string connectionString) {
      OperationMessage = OperationMessage.Of($"Connecting to {connectionString}");
      Broadcast();
      var cc = new object();
      _connectionCookie = cc;
      Task.Run(() => {
        try {
          var newClient = DeephavenClient.Client.Connect(connectionString, new ClientOptions());
          owner.Invoke(ts => ts.FinishConnect(cc, newClient, null));
        } catch (Exception ex) {
          owner.Invoke(ts => ts.FinishConnect(cc, null, ex));
        }
      });
    }

    private void FinishConnect(object expectedConnectionCookie, Client? newClient, Exception? exception) {
      if (expectedConnectionCookie != _connectionCookie) {
        newClient?.Dispose();
        return;
      }

      if (newClient != null) {
        OperationMessage = OperationMessage.Of(newClient);
      } else if (exception != null) {
        OperationMessage = OperationMessage.Of(exception.Message);
      } else {
        return;
      }

      Broadcast();
    }

    private void Broadcast() {
      foreach (var top in TableOperations) {
        // TODO(kosak): try-catch
        top.Stop();
      }

      foreach (var top in TableOperations) {
        // TODO(kosak): try-catch
        top.Start(OperationMessage);
      }
    }
  }
}
