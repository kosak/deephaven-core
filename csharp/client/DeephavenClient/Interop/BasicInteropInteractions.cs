using System;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient.Interop;

public partial class BasicInteropInteractions {
  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_Add(Int32 a, Int32 b, out Int32 result);

  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_AddArrays(Int32[] a, Int32[] b, Int32 length, Int32[] result);

  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_Xor(InteropBool a, InteropBool b, out InteropBool result);

  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_XorArrays(InteropBool[] a, InteropBool[] b, Int32 length, InteropBool[] result);

  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_Concat(string a, string b,
    out StringHandle resultHandle, out StringPoolHandle resultPoolHandle);

  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_ConcatArrays(
    string[] a,
    string[] b,
    Int32 numItems,
    StringHandle[] resultHandles,
    out StringPoolHandle resultPoolHandle);

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  public struct BasicStruct {
    public BasicStruct() {
    }

    public BasicStruct(int i, string? s) {
      this.i = i;
      this.s = s;
    }
    public int i;
    public string? s;
  }

  [DllImport(LibraryPaths.Dhcore, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_BasicStruct(
    ref BasicStruct data, int iOffset, string sAppend, out BasicStruct result);
}
