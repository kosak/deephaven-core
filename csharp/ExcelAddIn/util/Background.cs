﻿namespace Deephaven.ExcelAddIn;

internal static class Background666 {
  public static void Run(Action action) {
    throw new NotImplementedException("HELLO");
  }

  // public static void ClearAndDispose<T>(ref T? item) where T : class, IDisposable {
  //   (item, var todo) = (null, item);
  //   InvokeDispose(todo);
  // }

  public static void InvokeDispose(IDisposable? disp) {
    if (disp != null) {
      Run(disp.Dispose);
    }
  }
}
