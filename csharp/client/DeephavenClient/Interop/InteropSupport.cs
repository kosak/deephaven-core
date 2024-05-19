using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

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

public static class NativePtrUtil {
  public static bool TryRelease<T>(ref NativePtr<T> self, out NativePtr<T> oldPtr) {
    oldPtr = self;
    if (self.IsNull) {
      return false;
    }

    self = new NativePtr<T>(IntPtr.Zero);
    return true;
  }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct NativePtr<T> {
  public readonly IntPtr ptr;

  public NativePtr(IntPtr ptr) => this.ptr = ptr;

  public bool IsNull => ptr != IntPtr.Zero;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public readonly struct InteropBool : IEquatable<InteropBool> {
  private readonly sbyte _value;

  public InteropBool(bool value) { _value = value ? (sbyte)1 : (sbyte)0; }

  public bool BoolValue => _value != 0;

  public bool Equals(InteropBool other) {
    return _value == other._value;
  }

  public static explicit operator bool(InteropBool ib) => ib.BoolValue;
  public static explicit operator InteropBool(bool b) => new(b);
}

[StructLayout(LayoutKind.Sequential)]
public struct StringHandle {
  public Int32 Index;
}

[StructLayout(LayoutKind.Sequential)]
public struct StringPoolHandle {
  public NativePtr<NativeStringPool> NativeStringPool;
  public Int32 NumStrings;
  public Int32 NumBytes;

  public StringPool ImportAndDestroy() {
    if (!NativePtrUtil.TryRelease(ref NativeStringPool, out var temp)) {
      throw new InvalidOperationException("Can't run ImportAndDestroy twice");
    }

    var bytes = new byte[NumBytes];
    var ends = new Int32[NumStrings];
    NativeStringPool.deephaven_whatever_love_ImportAndDestroy(temp,
      bytes, bytes.Length,
      ends, ends.Length);

    var strings = new string[NumStrings];
    for (var i = 0; i != NumStrings; ++i) {
      var begin = i == 0 ? 0 : ends[i - 1];
      var end = ends[i];
      strings[i] = Encoding.UTF8.GetString(bytes, begin, end - begin);
    }

    return new StringPool(strings);
  }
}

public sealed class StringPool {
  public readonly string[] Strings;

  public StringPool(string[] strings) => Strings = strings;

  public string Get(StringHandle handle) {
    return Strings[handle.Index];
  }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct ErrorStatus {
  public string error;

  public readonly void OkOrThrow() {
    if (error != null) {
      throw new Exception(error);
    }
  }

  public readonly T Unwrap<T>(T item) {
    OkOrThrow();
    return item;
  }
}
