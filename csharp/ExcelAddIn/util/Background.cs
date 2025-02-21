namespace Deephaven.ExcelAddIn;

internal static class Background {
  public static void Run(Action action) {
    throw new NotImplementedException("HELLO");
  }

  // public static void ClearAndDispose<T>(ref T? item) where T : class, IDisposable {
  //   (item, var todo) = (null, item);
  //   if (todo != null) {
  //     Run(todo.Dispose);
  //   }
  // }
  public static void Dispose(IDisposable? disp) {
    if (disp != null) {
      Run(disp.Dispose);
    }
  }
}
