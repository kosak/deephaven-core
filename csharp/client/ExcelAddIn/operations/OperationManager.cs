namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

/// <summary>
/// In order to simplify the logic, the operations on this class (Register, Unregister,
/// Connect) are added to a queue and serviced by a dedicated thread.
/// </summary>
internal sealed class OperationManager {
  private readonly TableOperationManagerState _state = new();

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

  public void Disconnect() {
    _state.InvokeDisconnect();
  }

  private sealed class TableOperationManagerState {
    private readonly object _sync = new();
    /// <summary>
    /// Queue of lambdas to be applied to the TableOperationManagerState.
    /// </summary>
    private readonly Queue<Action> _actions = new();
    /// <summary>
    /// The current status message, or null. Exactly one of (_currentMessage, _currentClient)
    /// will always be set.
    /// </summary>
    private string _currentMessage = "Not connected to Deephaven";
    /// <summary>
    /// The current client, or null. Exactly one of (_currentMessage, _currentClient)
    /// will always be set.
    /// </summary>
    private Client? _currentClient = null;
    private readonly HashSet<IOperation> _tableOperations = new();
    private object _connectionCookie = new();

    public void StartThread() {
      new Thread(Doit) { IsBackground = true }.Start();
    }

    public void InvokeRegister(IOperation operation) {
      Invoke(() => {
        _tableOperations.Add(operation);
        SendCurrentStateTo(operation);
      });
    }

    public void InvokeUnregister(IOperation operation) {
      Invoke(() => {
        _tableOperations.Remove(operation);
        if (_currentClient != null) {
          operation.Stop();
        }
      });
    }

    public void InvokeConnect(string connectionString) {
      Invoke(() => StartConnect(connectionString));
    }

    public void InvokeDisconnect() {
      Invoke(Disconnect);
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

    private void StartConnect(string connectionString) {
      Disconnect();
      _currentMessage = $"Connecting to {connectionString}";
      BroadcastCurrentOperationMessage();
      var cc = new object();
      _connectionCookie = cc;
      // Because DeephavenClient.Client.Connect takes a long time, we do it in a separate thread.
      // If our user gets impatient, they may fire off a few StartConnects in a row.
      // To deal with this, we use the "_connectionCookie" to remember whether this is the
      // latest connection request, or some stale one that should just be discarded.
      Task.Run(() => {
        try {
          var newClient = DeephavenClient.Client.Connect(connectionString, new ClientOptions());
          Invoke(() => FinishConnectSuccessfully(cc, newClient));
        } catch (Exception ex) {
          Invoke(() => FinishConnectWithError(cc, ex));
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

    private void Disconnect() {
      if (_currentClient == null) {
        return;
      }

      var cc = _currentClient;
      _currentClient = null;
      _currentMessage = "Disconnected";
      foreach (var top in _tableOperations) {
        // TODO(kosak): try-catch
        top.Stop();
        top.Status(_currentMessage);
      }
      cc.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    private void BroadcastCurrentState() {
      foreach (var top in _tableOperations) {
        // TODO(kosak): try-catch
        top.Start(OperationMessage);
      }
    }
  }
}
