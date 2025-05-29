using Deephaven.ExcelAddIn.Observable;
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
}

internal class ExcelObserverWrapper(IExcelObserver inner) : IValueObserver<object?[,]> {
  public void OnNext(object?[,] value) {
    inner.OnNext(value);
  }
}
