﻿using Deephaven.ExcelAddIn.Util;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.ExcelDna;

internal class ExcelDnaHelpers {
  public static bool TryInterpretAs<T>(object value, T defaultValue, out T result) {
    result = defaultValue;
    if (value is ExcelMissing) {
      return true;
    }

    if (value is T tValue) {
      result = tValue;
      return true;
    }

    return false;
  }

  public static IObserver<StatusOr<object?[,]>> WrapExcelObserver(IExcelObserver inner) {
    return new ExcelObserverWrapper(inner);
  }

  private class ExcelObserverWrapper(IExcelObserver inner) : IObserver<StatusOr<object?[,]>> {
    public void OnNext(StatusOr<object?[,]> sov) {
      if (!sov.TryGetValue(out var value, out var status)) {
        value = new object[,] { { status } };
      }
      inner.OnNext(value);
    }

    public void OnCompleted() {
      inner.OnCompleted();
    }

    public void OnError(Exception error) {
      inner.OnError(error);
    }
  }
}