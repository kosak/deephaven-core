global using ICharColumnSource = Deephaven.ManagedClient.IColumnSource<char>;
global using IByteColumnSource = Deephaven.ManagedClient.IColumnSource<sbyte>;
global using IInt16ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int16>;
global using IInt32ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int32>;
global using IInt64ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int64>;
global using IFloatColumnSource = Deephaven.ManagedClient.IColumnSource<float>;
global using IDoubleColumnSource = Deephaven.ManagedClient.IColumnSource<double>;
global using IBooleanColumnSource = Deephaven.ManagedClient.IColumnSource<bool>;
global using IStringColumnSource = Deephaven.ManagedClient.IColumnSource<string>;
global using IDateTimeColumnSource = Deephaven.ManagedClient.IColumnSource<System.DateTime>;
global using IDateOnlyColumnSource = Deephaven.ManagedClient.IColumnSource<System.DateOnly>;
global using ITimeOnlyColumnSource = Deephaven.ManagedClient.IColumnSource<System.TimeOnly>;

namespace Deephaven.ExcelAddIn.Util;

internal static class Utility {
  public const string VersionString = "Version 1.0.0-snapshot";

  public static T Exchange<T>(ref T item, T newValue) {
    var result = item;
    item = newValue;
    return result;
  }

  // public static void MaybeDispose<T>(ref T? item) where T : class, IDisposable {
  //   var temp = Exchange(ref item, null);
  //   temp?.Dispose();
  // }

  public static T NotNull<T>(T? item) where T : class {
    if (item == null) {
      throw new ArgumentNullException();
    }
    return item;
  }

  public static void RunInBackground5(Action action) {
    throw new Exception("MEGA SAD");
  }
}

public class Unit {
  public static readonly Unit Instance = new ();

  private Unit() {
  }
}

public class ValueHolder<T> where T : class {
  private T? _value = null;

  public T Value {
    get {
      if (_value == null) {
        throw new Exception("Value is unset");
      }

      return _value;
    }
    set => _value = value;
  }
}
