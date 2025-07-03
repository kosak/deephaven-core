namespace Deephaven.Dh_NetClient;

public static class TaskUtil {
  public static T SaferGetResult<T>(Task<T> task) {
    return Task.Run(() => task.Result).Result;
  }

  public static T SaferGetResult<T>(ValueTask<T> task) {
    return Task.Run(() => task.Result).Result;
  }


  public static void SaferWait(Task task) {
    Task.Run(task.Wait).Wait();
  }
}
