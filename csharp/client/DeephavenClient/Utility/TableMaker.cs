using Deephaven.DeephavenClient.Interop;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient.Utility;

public class TableMaker : IDisposable {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_ctor(out NativePtr<NativeTableMaker> result,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhclient_utility_TableMaker_dtor(NativePtr<NativeTableMaker> self);

  private NativePtr<NativeTableMaker> _self;

  public TableMaker() {
    deephaven_dhclient_utility_TableMaker_ctor(out _self, out var status);
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
    deephaven_dhclient_utility_TableMaker_dtor(_self);
    _self.Reset();
  }

  public void AddColumn<T>(string name, IList<T> column) {
    var array = column.ToArray();
    var myVisitor = new MyVisitor(this);
    myVisitor.Visit(array);
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
    private TableMaker _owner;

    public void Visit(char[] array) {
      deephaven_dhclient_utility_TableMaker_AddColumn__Char(_owner._self, array);
    }

    public void Visit(sbyte[] array) {
      deephaven_dhclient_utility_TableMaker_AddColumn__Int8(_owner._self, array);
    }

    public void Visit(Int16[] array) {
      deephaven_dhclient_utility_TableMaker_AddColumn__Int16(_owner._self, array);
    }

    public void Visit(Int32[] array) {
      deephaven_dhclient_utility_TableMaker_AddColumn__Int32(_owner._self, array);
    }

    public void Visit(Int64[] array) {
      deephaven_dhclient_utility_TableMaker_AddColumn__Int64(_owner._self, array);
    }

    public void Visit(float[] array) {
      deephaven_dhclient_utility_TableMaker_AddColumn__Float(_owner._self, array);
    }

    public void Visit(double[] array) {
      deephaven_dhclient_utility_TableMaker_AddColumn__Double(_owner._self, array);
    }

    public void Visit(bool[] array) {
      var reinterpreted = new byte[array.Length];
      for (var i = 0; i != array.Length; ++i) {
        reinterpreted[i] = array[i] ? (byte)1 : (byte)0;
      }

      deephaven_dhclient_utility_TableMaker_AddColumn__BoolAsByte(_owner._self, reinterpreted);
    }

    public void Visit(string[] array) {
      deephaven_dhclient_utility_TableMaker_AddColumn__String(_owner._self, array);
    }

    public void Visit(DateTime[] array) {
      var reinterpreted = new long[array.Length];
      for (var i = 0; i != array.Length; ++i) {
        reinterpreted[i] = array[i].Nanosecond;
      }

      deephaven_dhclient_utility_TableMaker_AddColumn__DateTimeAsLong(_owner._self, reinterpreted);
    }
  }
}

/**
 * Placeholder for NativePtr
 */
internal class NativeTableMaker {
}
