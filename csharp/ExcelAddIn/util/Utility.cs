global using ICharColumnSource = Deephaven.Dh_NetClient.IColumnSource<char>;
global using IByteColumnSource = Deephaven.Dh_NetClient.IColumnSource<sbyte>;
global using IInt16ColumnSource = Deephaven.Dh_NetClient.IColumnSource<System.Int16>;
global using IInt32ColumnSource = Deephaven.Dh_NetClient.IColumnSource<System.Int32>;
global using IInt64ColumnSource = Deephaven.Dh_NetClient.IColumnSource<System.Int64>;
global using IFloatColumnSource = Deephaven.Dh_NetClient.IColumnSource<float>;
global using IDoubleColumnSource = Deephaven.Dh_NetClient.IColumnSource<double>;
global using IBooleanColumnSource = Deephaven.Dh_NetClient.IColumnSource<bool>;
global using IStringColumnSource = Deephaven.Dh_NetClient.IColumnSource<string>;
global using IDateTimeOffsetColumnSource = Deephaven.Dh_NetClient.IColumnSource<System.DateTimeOffset>;
global using IDateOnlyColumnSource = Deephaven.Dh_NetClient.IColumnSource<System.DateOnly>;
global using ITimeOnlyColumnSource = Deephaven.Dh_NetClient.IColumnSource<System.TimeOnly>;

namespace Deephaven.ExcelAddIn.Util;

internal static class Utility {
  public const string VersionString = "Version 1.0.3";

  public static void ClearAndDispose<T>(ref T? item) where T : class, IDisposable {
    var temp = Exchange(ref item, null);
    temp?.Dispose();
  }

  public static T NotNull<T>(T? item) where T : class {
    if (item == null) {
      throw new ArgumentNullException();
    }
    return item;
  }

  public static T Exchange<T>(ref T location, T value) {
    var oldValue = location;
    location = value;
    return oldValue;
  }
}

public class Unit {
  public static readonly Unit Instance = new ();

  private Unit() {
  }
}
