using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
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

internal class ExcelObserverWrapper(IExcelObserver inner) : IValueObserver<StatusOr<object?[,]>> {
  public void OnNext(StatusOr<object?[,]> sov) {
    if (!sov.GetValueOrStatus(out var value, out var status)) {
      // Reformat the status text as an object[,] 2D array so Excel renders it as 1x1 "table".
      var text = status.Text;
      // TODO(kosak): return Excel st atuses here and optionally decorate the
      // calling cell with the state.
      value = new object[,] { { text } };
    }
    inner.OnNext(value);
  }
}
