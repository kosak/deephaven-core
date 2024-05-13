using System;
using System.Runtime.InteropServices;

namespace Deephaven.DeephavenClient.Interop;

internal class LibraryPaths {
  internal const string Dhcore = "dhcore.dll";
  internal const string Dhclient = "dhclient.dll";
}

public class PlatformUtf16 {
  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  public delegate void AllocatorHelper(
    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] inItems,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] outItems,
    int count);

  [DllImport(LibraryPaths.Dhcore, CharSet = CharSet.Unicode)]
  internal static extern void deephaven_dhcore_interop_PlatformUtf16_register_allocator_helper(AllocatorHelper allocator);

  private static void BulkIdentity(string[] inItems, string[] outItems, Int32 count) {
    if (count == 0) {
      return;
    }
    Console.WriteLine($"Array has length {inItems.Length} and {outItems.Length}. Count i s{count}");
    for (int i = 0; i < count; ++i) {
      Console.WriteLine($"BulkIdentity in item {i} is {inItems[i]}");
    }
    for (int i = 0; i < count; ++i) {
      outItems[i] = inItems[i];
    }
  }

  public static void Init() {
    deephaven_dhcore_interop_PlatformUtf16_register_allocator_helper(BulkIdentity);
  }
}

[StructLayout(LayoutKind.Sequential)]
public struct NativePtr<T> {
  public IntPtr ptr;

  public NativePtr(IntPtr ptr) => this.ptr = ptr;

  public bool IsNull => ptr != IntPtr.Zero;

  public NativePtr<T> Release() {
    var result = new NativePtr<T>(ptr);
    ptr = IntPtr.Zero;
    return result;
  }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct ErrorStatus {
  public string error;

  public void OkOrThrow() {
    if (error != null) {
      throw new Exception(error);
    }
  }

  public T Unwrap<T>(T item) {
    OkOrThrow();
    return item;
  }
}
