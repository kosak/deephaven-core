namespace Deephaven.ExcelAddIn;

internal static class Background {
  public static void Run(Action action) {
    throw new NotImplementedException("HELLO");
  }

  public static void ClearAndDispose(ref IDisposable? item) {
    (item, var todo) = (null, item);
    if (todo != null) {
      Run(todo.Dispose);
    }
  }
}
