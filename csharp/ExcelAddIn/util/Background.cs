using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn;

internal static class Background {
  public static void Run(Action action) {
    Task.Run(action).Forget();
  }
}
