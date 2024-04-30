using System;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient.Interop;

class Constants {
  public const string DhCorePath = "dhcore.dll";
  public const string DhClientPath = "dhclient.dll";
}

public class BasicInteropInteractions {
  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Add(Int32 a, Int32 b, out Int32 result);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Concat(string a, string b, out string result);

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

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_BasicStruct(
    int i, string s, int iOffset, string sAppend, out BasicStruct result);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ArrayRunningSum(
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] Int32[] data,
    Int32 length,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
    Int32[] result);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] string[] data,
    Int32 length,
    string toAppend,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
    string[] result);
}
