using System;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient.Interop;

public class BasicInteropInteractions {
  [DllImport(LibraryPaths.Dhcore, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Add(Int32 a, Int32 b, out Int32 result);

  [DllImport(LibraryPaths.Dhcore, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Concat(string a, string b,
    out string result);

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

  [DllImport(LibraryPaths.Dhcore, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ArrayRunningSum(
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] Int32[] data,
    Int32 length,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
    Int32[] result);

  [DllImport(LibraryPaths.Dhcore, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] string[] data,
    Int32 length,
    string toAppend,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
    string[] result);

  [DllImport(LibraryPaths.Dhcore, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Less(Int32 a, Int32 b, out InteropBool result);

  [DllImport(LibraryPaths.Dhcore, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Less_Array(
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
    Int32[] a,
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
    Int32[] b,
    Int32 length,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
    InteropBool[] results);
}
