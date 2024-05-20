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

  [StructLayout(LayoutKind.Sequential)]
  public struct BasicStruct {
    public BasicStruct() {
    }

    public BasicStruct(int i, double d) {
      this.i = i;
      this.d = d;
    }
    public int i;
    public double d;
  }

  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_AddBasicStruct(
    ref BasicStruct a, ref BasicStruct b, out BasicStruct result);
  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_AddBasicStructArrays(
    BasicStruct[] a, BasicStruct[] b, Int32 length, BasicStruct[] result);

  [StructLayout(LayoutKind.Sequential)]
  public struct NestedStruct {
    public BasicStruct A;
    public BasicStruct B;

    public NestedStruct(BasicStruct a, BasicStruct b) {
      A = a;
      B = b;
    }
  }

  [LibraryImport(LibraryPaths.Dhcore, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void deephaven_dhcore_basicInteropInteractions_AddNestedStruct(
    ref NestedStruct a, ref NestedStruct b, out NestedStruct result);
}
