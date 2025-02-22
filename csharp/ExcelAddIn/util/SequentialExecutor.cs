using System.Diagnostics;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Util;

public class SequentialExecutor {
  private readonly object _sync = new();
  private List<Action> _todo = new();
  private bool _serviceThreadStarted = false;
  private Thread? _serviceThread = null;

  /**
   * Enqueue an action onto the servicing queue and start a thread pool task if
   * one is not already running.
   */
  public void Run(Action action) {
    bool needToStart;
    lock (_sync) {
      _todo.Add(action);
      needToStart = _serviceThreadStarted;
      _serviceThreadStarted = true;
    }

    if (needToStart) {
      Task.Run(ServiceLoop).Forget();
    }
  }

  /**
   * If the service thread is running and the caller is on that very thread,
   * do nothing and return false.  Otherwise, call Enqueue()
   */
  public bool EnqueueOrNop(Action action) {
    lock (_sync) {
      if (ReferenceEquals(Thread.CurrentThread, _serviceThread)) {
        // We are already running on the service thread. Do nothing and return false.
        return false;
      }
    }
    Enqueue(action);
    // Action was enqueued on the service thread. Return true.
    return true;
  }

  /**
   * If the service thread is running and the caller is on that very thread,
   * invoke the action directly. Otherwise, call Enqueue()
   */
  public void EnqueueOrRun(Action action) {
    if (!EnqueueOrNop(action)) {
      action();
    }
  }

  /**
   * Create a Disposable whose Dispose operation invokes Enqueue
   */
  public IDisposable EnqueueWhenDisposed(Action action) {
    return ActionAsDisposable.Create(() => Enqueue(action));
  }

  /**
 * Create a Disposable whose Dispose operation invokes EnqueueOrRun
 */
  public IDisposable EnqueueOrRunWhenDisposed(Action action) {
    return ActionAsDisposable.Create(() => EnqueueOrRun(action));
  }

  private void ServiceLoop() {
    lock (_sync) {
      _serviceThread = Thread.CurrentThread;
    }
    while (true) {
      List<Action> localTodo;
      lock (_sync) {
        if (_todo.Count == 0) {
          _serviceThread = null;
          _serviceThreadStarted = false;
          return;
        }

        localTodo = _todo;
        _todo = new();
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
