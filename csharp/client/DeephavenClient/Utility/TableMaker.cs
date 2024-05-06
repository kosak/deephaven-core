using Deephaven.DeephavenClient.Interop;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient.Utility;

public class TableMaker : IDisposable {
  private NativePtr<NativeTableMaker> _self;

  public TableMaker() {
    NativeTableMaker.deephaven_dhclient_utility_TableMaker_ctor(out _self, out var status);
    status.OkOrThrow();
  }

  ~TableMaker() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  public void AddColumn<T>(string name, IList<T> column) {
    var array = column.ToArray();
    var myVisitor = new MyVisitor(this, name);
    ArrayDispatcher.AcceptVisitor(myVisitor, array);
  }

  public TableHandle MakeTable(TableHandleManager manager) {
    NativeTableMaker.deephaven_dhclient_utility_TableMaker_MakeTable(_self, manager._self, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, manager);
  }

  private void ReleaseUnmanagedResources() {
    var temp = _self.Reset();
    if (temp.IsNull) {
      return;
    }
    NativeTableMaker.deephaven_dhclient_utility_TableMaker_dtor(temp);
  }

  private static class ArrayDispatcher {
    public static void AcceptVisitor<T>(IArrayVisitor visitor, T[] array) {
      // TODO: make this faster
      if (array is char[] chars) {
        visitor.Visit(chars);
        return;
      }

      if (array is sbyte[] sbytes) {
        visitor.Visit(sbytes);
        return;
      }

      if (array is Int16[] int16s) {
        visitor.Visit(int16s);
        return;
      }

      if (array is Int32[] int32s) {
        visitor.Visit(int32s);
        return;
      }

      if (array is Int64[] int64s) {
        visitor.Visit(int64s);
        return;
      }

      if (array is float[] floats) {
        visitor.Visit(floats);
        return;
      }

      if (array is double[] doubles) {
        visitor.Visit(doubles);
        return;
      }

      if (array is bool[] bools) {
        visitor.Visit(bools);
        return;
      }

      if (array is string[] strings) {
        visitor.Visit(strings);
        return;
      }

      if (array is DateTime[] datetimes) {
        visitor.Visit(datetimes);
        return;
      }

      throw new ArgumentException($"Don't know how to handle type {array.GetType().Name}");
    }
  }

  // put this somewhere
  private interface IArrayVisitor {
    public void Visit(char[] array);
    public void Visit(sbyte[] array);
    public void Visit(Int16[] array);
    public void Visit(Int32[] array);
    public void Visit(Int64[] array);
    public void Visit(float[] array);
    public void Visit(double[] array);
    public void Visit(bool[] array);
    public void Visit(string[] array);
    public void Visit(DateTime[] array);
  }

  private class MyVisitor : IArrayVisitor {
    private readonly TableMaker _owner;
    private readonly string _name;

    public MyVisitor(TableMaker owner, string name) {
      _owner = owner;
      _name = name;
    }

    public void Visit(char[] array) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Char(
        _owner._self, _name, array, array.Length, out var status);
      status.OkOrThrow();
    }

    public void Visit(sbyte[] array) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Int8(
        _owner._self, _name, array, array.Length, out var status);
      status.OkOrThrow(); 
    }

    public void Visit(Int16[] array) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Int16(
        _owner._self, _name, array, array.Length, out var status);
      status.OkOrThrow();
    }

    public void Visit(Int32[] array) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Int32(
        _owner._self, _name, array, array.Length, out var status);
      status.OkOrThrow();
    }

    public void Visit(Int64[] array) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Int64(
        _owner._self, _name, array, array.Length, out var status);
      status.OkOrThrow();
    }

    public void Visit(float[] array) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Float(
        _owner._self, _name, array, array.Length, out var status);
      status.OkOrThrow();
    }

    public void Visit(double[] array) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Double(
        _owner._self, _name, array, array.Length, out var status);
      status.OkOrThrow();
    }

    public void Visit(bool[] array) {
      var reinterpreted = new byte[array.Length];
      for (var i = 0; i != array.Length; ++i) {
        reinterpreted[i] = array[i] ? (byte)1 : (byte)0;
      }

     NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__BoolAsByte(
        _owner._self, _name, reinterpreted, reinterpreted.Length, out var status);
      status.OkOrThrow();
    }

    public void Visit(string[] array) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__String(
        _owner._self, _name, array, array.Length, out var status);
      status.OkOrThrow();
    }

    public void Visit(DateTime[] array) {
      var reinterpreted = new long[array.Length];
      for (var i = 0; i != array.Length; ++i) {
        reinterpreted[i] = array[i].Nanosecond;
      }

      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__DateTimeAsLong(
        _owner._self, _name, reinterpreted, reinterpreted.Length, out var status);
      status.OkOrThrow();
    }
  }
}

/**
 * Placeholder for use with NativePtr
 */
internal class NativeTableMaker {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_ctor(out NativePtr<NativeTableMaker> result,
  out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_dtor(NativePtr<NativeTableMaker> self);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_MakeTable(NativePtr<NativeTableMaker> self,
    NativePtr<NativeTableHandleManager> manager,
    out NativePtr<NativeTableHandle> result,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Char(
    NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    char[] data,
    Int32 length,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Int8(
      NativePtr<NativeTableMaker> self,
      string name,
      [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[] data,
      Int32 length,
      out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Int16(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    Int16[] data,
    Int32 length,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Int32(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    Int32[] data,
    Int32 length,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Int64(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    Int64[] data,
    Int32 length,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Float(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    float[] data,
    Int32 length,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Double(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    double[] data,
    Int32 length,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__BoolAsByte(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    byte[] data,
    Int32 length,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__String(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    string[] data,
    Int32 length,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__DateTimeAsLong(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    Int64[] data,
    Int32 length,
    out ErrorStatus status);
}
