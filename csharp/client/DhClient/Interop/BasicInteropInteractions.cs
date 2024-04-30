using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace Deephaven.DhClient.Interop;

class Constants {
  public const string DhCorePath = "dhcore.dll";
  public const string DhClientPath = "dhclient.dll";
}

public class BasicInteropInteractions {
  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Add(Int32 a, Int32 b, out Int32 result);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_AddInPlace(ref Int32 value, Int32 offset);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Concat(string a, string b, out string result);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ConcatInPlace(ref string s, string toAppend);

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  public struct BasicStruct {
    public int i;
    public string s;
  }

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_BasicStruct(
    int i, string s, int iOffset, string sAppend, out BasicStruct result);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_BasicStructInPlace(
    ref BasicStruct s, int iOffset, string sAppend);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ArrayRunningSum(
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] Int32[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] Int32[] result,
    Int32 length);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ArrayRunningSumInPlace(
    [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] Int32[] data,
    Int32 length);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] data,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] result,
    Int32 length);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_ArrayElementConcatInPlace(
    [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] string[] data,
    Int32 length);
}
