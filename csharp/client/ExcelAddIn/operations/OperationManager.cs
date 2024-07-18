namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

/// <summary>
/// In order to simplify the logic, the operations on this class (Register, Unregister,
/// Connect) are added to a queue and serviced by a dedicated thread.
/// </summary>
internal sealed class OperationManager {
  private TableOperationManagerState _state = new();

  public OperationManager() {
    _state.StartThread();
  }

  public void Register(IOperation operation) {
    _state.InvokeRegister(operation);
  }

  public void Unregister(IOperation operation) {
    _state.InvokeUnregister(operation);
  }

  public void Connect(string connectionString) {
    _state.InvokeConnect(connectionString);
  }

  private sealed class TableOperationManagerState {
    private readonly object _sync = new();
    /// <summary>
    /// Queue of lambdas to be applied to the TableOperationManagerState.
    /// </summary>
    private readonly Queue<Action> _actions = new();

    private NewClientOrStatus _operationMessage = NewClientOrStatus.Of("Not connected to Deephaven");
    private readonly HashSet<IOperation> _tableOperations = new();
    private object _connectionCookie = new();

    public void StartThread() {
      new Thread(Doit) { IsBackground = true }.Start();
    }

    public void InvokeRegister(IOperation operation) {
      Invoke(() => {
        _tableOperations.Add(operation);
        operation.Start(_operationMessage);
      });
    }

    public void InvokeUnregister(IOperation operation) {
      Invoke(() => {
        _tableOperations.Remove(operation);
        operation.Stop();
      });
    }

    public void InvokeConnect(string connectionString) {
      Invoke(() => StartConnect(connectionString));
    }

    private void Invoke(Action a) {
      lock (_sync) {
        _actions.Enqueue(a);
        Monitor.PulseAll(_sync);
      }
    }

    /// <summary>
    /// This runs in a dedicated thread, waiting for actions to be added to the _actions queue.
    /// </summary>
    private void Doit() {
      while (true) {
        Action? action;
        lock (_sync) {
          while (true) {
            if (_actions.TryDequeue(out action)) {
              break;
            }

            Monitor.Wait(_sync);
          }
        }

        // Invoke the action while not holding the lock.
        action();
      }
    }

    private void StartConnect(OperationManager owner, string connectionString) {
      _operationMessage = NewClientOrStatus.Of($"Connecting to {connectionString}");
      BroadcastCurrentOperationMessage();
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
        OperationMessage = NewClientOrStatus.Of(newClient);
      } else if (exception != null) {
        OperationMessage = NewClientOrStatus.Of(exception.Message);
      } else {
        return;
      }

      Broadcast();
    }

    /// <summary>
    /// 
    /// </summary>
    private void BroadcastCurrentOperationMessage() {
      foreach (var top in _tableOperations) {
        // TODO(kosak): try-catch
        top.Stop();
      }

      foreach (var top in _tableOperations) {
        // TODO(kosak): try-catch
        top.Start(OperationMessage);
      }
    }
  }
}
