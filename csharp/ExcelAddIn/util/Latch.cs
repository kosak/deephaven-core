namespace Deephaven.ExcelAddIn.Util;

internal class Latch {
  private bool _value;

  public bool TrySet() {
    if (_value) {
      return false;
    }
    _value = true;
    return true;
  }

  public bool Value => _value;
}
