namespace Deephaven.Dh_NetClient;

public static class TaskUtil {
  public static T SaferGetResult<T>(Func<Task<T>> func) {
    return Task.Run(() => func().Result).Result;
  }

  public static T SaferGetResult<T>(Func<ValueTask<T>> func) {
    return Task.Run(() => func().Result).Result;
  }

  public static void SaferWait(Func<Task> func) {
    Task.Run(() => func().Wait()).Wait();
  }
}
