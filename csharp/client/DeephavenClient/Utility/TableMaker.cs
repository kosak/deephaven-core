using System.Buffers;
using Deephaven.DeephavenClient.Interop;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient.Utility;

public class TableMaker : IDisposable {
  internal NativePtr<NativeTableMaker> Self;

  public TableMaker() {
    NativeTableMaker.deephaven_dhclient_utility_TableMaker_ctor(out Self, out var status);
    status.OkOrThrow();
  }

  ~TableMaker() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  private void ReleaseUnmanagedResources() {
    if (!NativePtrUtil.TryRelease(ref Self, out var old)) {
      return;
    }
    NativeTableMaker.deephaven_dhclient_utility_TableMaker_dtor(old);
  }

  public void AddColumn<T>(string name, IList<T> column) {
    var array = column.ToArray();
    var myVisitor = new MyVisitor(this, name);
    ArrayDispatcher.AcceptVisitor(myVisitor, array);
  }

  public TableHandle MakeTable(TableHandleManager manager) {
    NativeTableMaker.deephaven_dhclient_utility_TableMaker_MakeTable(Self, manager.Self, out var result, out var status);
    status.OkOrThrow();
    return new TableHandle(result, manager);
  }

  private static class ArrayDispatcher {
    static void ConvertOptional<T>(T?[] input, out T[] data, out sbyte[] nulls) where T : struct {
      data = new T[input.Length];
      nulls = new sbyte[input.Length];
      for (var i = 0; i != input.Length; ++i) {
        if (input[i].HasValue) {
          data[i] = input[i]!.Value;
        } else {
          nulls[i] = 1;
        }
      }
    }

    public static void AcceptVisitor<T>(IArrayVisitor visitor, T[] array) {
      // TODO: make this faster
      if (array is char[] chars) {
        visitor.Visit(chars, null);
        return;
      }

      if (array is char?[] optionalChars) {
        ConvertOptional(optionalChars, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is sbyte[] sbytes) {
        visitor.Visit(sbytes, null);
        return;
      }

      if (array is sbyte?[] optionalSbytes) {
        ConvertOptional(optionalSbytes, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is Int16[] int16s) {
        visitor.Visit(int16s, null);
        return;
      }

      if (array is Int16?[] optionalInt16s) {
        ConvertOptional(optionalInt16s, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is Int32[] int32s) {
        visitor.Visit(int32s, null);
        return;
      }

      if (array is Int32?[] optionalInt32s) {
        ConvertOptional(optionalInt32s, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is Int64[] int64s) {
        visitor.Visit(int64s, null);
        return;
      }

      if (array is Int64?[] optionalInt64s) {
        ConvertOptional(optionalInt64s, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is float[] floats) {
        visitor.Visit(floats, null);
        return;
      }

      if (array is float?[] optionalFloats) {
        ConvertOptional(optionalFloats, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is double[] doubles) {
        visitor.Visit(doubles, null);
        return;
      }

      if (array is float?[] optionalDoubles) {
        ConvertOptional(optionalDoubles, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is bool[] bools) {
        visitor.Visit(bools, null);
        return;
      }

      if (array is bool?[] optionalBools) {
        ConvertOptional(optionalBools, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is DhDateTime[] datetimes) {
        visitor.Visit(datetimes, null);
        return;
      }

      if (array is DhDateTime?[] optionalDateTimes) {
        ConvertOptional(optionalDateTimes, out var data, out var nulls);
        visitor.Visit(data, nulls);
        return;
      }

      if (array is string[] strings) {
        visitor.Visit(strings);
        return;
      }

      throw new ArgumentException($"Don't know how to handle type {array.GetType().Name}");
    }
  }

  // put this somewhere
  private interface IArrayVisitor {
    public void Visit(char[] array, sbyte[]? nulls);
    public void Visit(sbyte[] array, sbyte[]? nulls);
    public void Visit(Int16[] array, sbyte[]? nulls);
    public void Visit(Int32[] array, sbyte[]? nulls);
    public void Visit(Int64[] array, sbyte[]? nulls);
    public void Visit(float[] array, sbyte[]? nulls);
    public void Visit(double[] array, sbyte[]? nulls);
    public void Visit(bool[] array, sbyte[]? nulls);
    public void Visit(DhDateTime[] array, sbyte[]? nulls);
    // No nulls array because string is a reference type
    public void Visit(string[] array);
  }

  private class MyVisitor : IArrayVisitor {
    private readonly TableMaker _owner;
    private readonly string _name;

    public MyVisitor(TableMaker owner, string name) {
      _owner = owner;
      _name = name;
    }

    public void Visit(char[] array, sbyte[]? nulls) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Char(
        _owner.Self, _name, array, array.Length, nulls, out var status);
      status.OkOrThrow();
    }

    public void Visit(sbyte[] array, sbyte[]? nulls) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Int8(
        _owner.Self, _name, array, array.Length, nulls, out var status);
      status.OkOrThrow(); 
    }

    public void Visit(Int16[] array, sbyte[]? nulls) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Int16(
        _owner.Self, _name, array, array.Length, nulls, out var status);
      status.OkOrThrow();
    }

    public void Visit(Int32[] array, sbyte[]? nulls) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Int32(
        _owner.Self, _name, array, array.Length, nulls, out var status);
      status.OkOrThrow();
    }

    public void Visit(Int64[] array, sbyte[]? nulls) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Int64(
        _owner.Self, _name, array, array.Length, nulls, out var status);
      status.OkOrThrow();
    }

    public void Visit(float[] array, sbyte[]? nulls) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Float(
        _owner.Self, _name, array, array.Length, nulls, out var status);
      status.OkOrThrow();
    }

    public void Visit(double[] array, sbyte[]? nulls) {
      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__Double(
        _owner.Self, _name, array, array.Length, nulls, out var status);
      status.OkOrThrow();
    }

    public void Visit(bool[] array, sbyte[]? nulls) {
      var reinterpreted = new byte[array.Length];
      for (var i = 0; i != array.Length; ++i) {
        reinterpreted[i] = array[i] ? (byte)1 : (byte)0;
      }

      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__BoolAsByte(
        _owner.Self, _name, reinterpreted, reinterpreted.Length, nulls, out var status);
      status.OkOrThrow();
    }

    public void Visit(string?[] array) {
      var nulls = new sbyte[array.Length];
      for (var i = 0; i != array.Length; ++i) {
        nulls[i] = array[i] == null ? (sbyte)1 : (sbyte)0;
      }

      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__String(
        _owner.Self, _name, array, array.Length, nulls, out var status);
      status.OkOrThrow();
    }

    public void Visit(DhDateTime[] array, sbyte[]? nulls) {
      var reinterpreted = new long[array.Length];
      for (var i = 0; i != array.Length; ++i) {
        reinterpreted[i] = array[i].Nanos;
      }

      NativeTableMaker.deephaven_dhclient_utility_TableMaker_AddColumn__DateTimeAsLong(
        _owner.Self, _name, reinterpreted, reinterpreted.Length, nulls, out var status);
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
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Int8(
    NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[] data,
    Int32 length,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Int16(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    Int16[] data,
    Int32 length,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Int32(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    Int32[] data,
    Int32 length,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Int64(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    Int64[] data,
    Int32 length,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Float(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    float[] data,
    Int32 length,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__Double(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    double[] data,
    Int32 length,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__BoolAsByte(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    byte[] data,
    Int32 length,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__DateTimeAsLong(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    Int64[] data,
    Int32 length,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    sbyte[]? nulls,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_AddColumn__String(NativePtr<NativeTableMaker> self,
    string name,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
    string?[] data,
    Int32 length,
    sbyte[]? nulls,
    out ErrorStatus status);
}
