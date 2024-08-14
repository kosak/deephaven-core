using ExcelDna.Integration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.ExcelAddIn.ExcelDna;

internal class ExcelDnaUtil {
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
