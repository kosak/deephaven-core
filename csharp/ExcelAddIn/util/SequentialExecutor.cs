using System.Diagnostics;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Util;

public class SequentialExecutor {
  private static readonly SequentialExecutor SingleGlobalInstanceDebugging = new();

  public static SequentialExecutor Create() {
    return SingleGlobalInstanceDebugging;
  }

  private readonly object _sync = new();
  private List<Action> _todo = new();
  private bool _serviceThreadExists = false;

  private SequentialExecutor() {

  }

  /**
   * Enqueue an action onto the servicing queue and start a thread pool task if
   * one is not already running.
   */
  public void Run(Action action) {
    bool oldValue;
    lock (_sync) {
      _todo.Add(action);
      oldValue = _serviceThreadExists;
      _serviceThreadExists = true;
    }

    if (!oldValue) {
      Task.Run(ServiceLoop).Forget();
    }
  }

  private void ServiceLoop() {
    while (true) {
      List<Action> localTodo;
      lock (_sync) {
        if (_todo.Count == 0) {
          _serviceThreadExists = false;
          return;
        }

        (localTodo, _todo) = (_todo, new());
      }

      foreach (var action in localTodo) {
        try {
          action();
        } catch (Exception ex) {
          Debug.WriteLine($"Swallowing exception {ex}");
        }
      }
    }
  }
}
