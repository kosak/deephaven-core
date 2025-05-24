using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Util;

internal static class Background {
  public static void Run(Action action) {
    Task.Run(action).Forget();
  }

  public static void InvokeDispose(IDisposable? disp) {
    if (disp == null) {
      return;
    }
    Run(disp.Dispose);
  }
}
