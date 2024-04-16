/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#pragma once

#include <codecvt>
#include <cstdint>
#include <exception>
#include <iostream>
#include <locale>
#include <string>
#include <string_view>

namespace deephaven::dhcore::interop {
using Utf16Converter = std::wstring_convert<std::codecvt_utf8_utf16<char16_t>, char16_t>;

/**
 * This is a special class that we use to help us remember to return the right
 * kind of string to the platform (e.g. Windows) without making careless programming mistakes.
 *
 * The story here is complicated.
 *
 * 1. We have standardized on UTF16, meaning that all of our P/Invoke attributes have
 *    CharSet = CharSet.Unicode
 * 2. Strings being passed to us come in as const char 16_t *. We typically immediately convert
 *    them to UTF-8 before doing any further processing.
 * 3. Strings that we return are passed as "out" parameters. Conceptually this is a
 *    const char16_t **  (however we use a different type than char16_t... see below).
 * 4. An array of strings is also returned as an "out" parameter. Conceptually this is a
 *    const char16_t ** (just as above) plus a length argument. The caller allocates the array
 *    and uses MarshalAs(UnmanagedType.LPArray, SizeParamIndex = N)] to communicate the needed
 *    information to the marshalling system. Again as above the type we actually use is different
 *    from char16_t. See below
 *
 * As always, one must be mindful of memory ownership. For strings being passed to us, this is
 * not a problem (caller owns the storage). For strings that we give back to the caller, either
 * as a single string or an array of strings, the situation is more complicated because we need
 * to allocate the string and the caller needs to free it. This needs to be done in a platform-
 * specific way. On Windows this is via CoTaskMemAlloc, and on Linux it is via normal malloc.
 *
 * However, we are able to borrow a trick from SWIG to avoid this issue altogether. The trick is
 * based on the observation that when we call back to managed code via a function pointer
 * (aka a delegate in C#), the roles are reversed: we manage the storage for a string that we
 * pass to the callback; but if the callback returns a string to us, it has to follow the
 * special allocation rules.
 *
 * The trick is to make our caller write an "identity function" and give us a pointer (delegate)
 * to it that we keep around for the lifetime of the program.
 *
 * That identity function looks like this in C#
 *
 * // inStrings.Length == outStrings.Length == count
 * // 'count' is needed for the marshalling infrastructure
 * static void PlatformAllocatorHelper(string[] inStrings, string[] outStrings, int count) {
 *   assert(inStrings.Length == count && outStrings.Length == count);
 *   for (int i = 0; i < count; ++i) {
 *     outStrings[i] = inStrings[i];
 *   }
 * }
 *
 * As you can see it merely copies input to output, and it works on arrays rather than single
 * strings for a bit more efficiency when doing bulk operations.
 *
 * The delegate that points to this method is declared in a very careful way:
 *
 * [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
 * public delegate void PlatformAllocatorHelperDelegate(
 *   [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] inStrings,
 *   [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] outStrings,
 *   int count);
 *
 * At application startup time this delegate is passed to the C++ side and turned into a
 * PlatformUtf16::platformAllocatorHelper_t, and stored as a static member of our class.
 * As an implementation detail, we store it in a static accessor function rather than a global
 * variable because avoiding globals allows us to build shared libraries more easily with Windows.
 *
 * Finally we get to special note alluded to above. It would be an easy mistake to make if we
 * return the "wrong" kind of char16_t *... that is, one that had not been allocated with the
 * above process and therefore having the wrong ownership semantics. To reduce the chance of
 * this, all of our returned strings are of type "PlatformUtf16". THis is a class containing
 * only one member, of type char16_t *. Thanks to the C calling convention and type punning,
 * Windows doesn't know that this isn't just a plain old char16_t*. But on the C++ side, we
 * can only create objects of this type with a factory method so we can't forget to allocate
 * them properly. Furthermore the signatures of our methods are also written differently.
 * Consider the hypothetical function GiveMeAString that returns a string as an 'out'
 * parameter. Instead of writing:
 *    void GiveMeAString(char16_t **result);
 * we would write:
 *    void GiveMeAString(PlatformUtf16 *result);
 *
 * On the Windows side, the extern declaration for GiveMeAString still looks the same:
 *
 * [DllImport("Dhclient.dll", CharSet = CharSet.Unicode)]
 * public static extern void GiveMeAString(out string result);
 *
 * Also note that we generally use 'out' parameters rather than return values. The reason is
 * that most of our methods may return more than one value, and we try to keep things simple
 * rather than return structured types. A typical method looks more like this:
 *
 * [DllImport("Dhclient.dll", CharSet = CharSet.Unicode)]
 * public static extern void GiveMeStuff(int a, int b, int c,
 *   [Out] int x, [Out] int y, [Out] string z, [Out] ErrorStatus status);
 *
 * Where [Out] is the P/Invoke attribute for out parameters, which is different from the
 * C# "out" keyword.
 *
 * And also we include a final ErrorStatus out argument, which is our convention to return
 * overall success or failure. This is similar to an Arrow::Status type but much simpler.
 */
class PlatformUtf16 {
public:
  using allocatorHelper_t = void (*)(const char16_t **in_items, const char16_t **out_items,
      int32_t count);
  /**
   * This could have been a static variable. However the CMake tool that does this automatically
   * with CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON works only with functions and not with global
   * variables. So in order to avoid doing extra work to dllimport/dllexport this symbol, we
   * turn it into a function.
   */
  static allocatorHelper_t &AllocatorHelper();

  static PlatformUtf16 Create(std::string_view item);
  static PlatformUtf16 Create(std::u16string_view  item);
  static void CreateBulk(const std::string *strings, size_t num_strings, PlatformUtf16 *results);
  static void CreateBulk(const std::u16string *strings, size_t num_strings, PlatformUtf16 *results);

  void Reset() {
    data_ = nullptr;
  }

private:
  explicit PlatformUtf16(const char16_t *data) : data_(data) {}

  const char16_t *data_ = nullptr;
};

class ErrorStatus {
public:
  /**
   * This is used to wrap caller code in a lambda so that we can automatically set the error
   * status if the lambda throws an exception.
   */
  template<typename T>
  void Run(const T &callback) {
    text_.Reset();
    try {
      // Sanity check for this method's callers to make sure they're not returning a value
      // which would be a programming mistake because it is ignored here.
      static_assert(std::is_same_v<decltype(callback()), void>);
      callback();
    } catch (const std::exception &e) {
      text_ = PlatformUtf16::Create(e.what());
    } catch (...) {
      text_ = PlatformUtf16::Create("Unknown exception");
    }
  }

private:
  PlatformUtf16 text_;
};
}  // namespace deephaven::dhcore::interop

extern "C" {
void deephaven_dhcore_interop_PlatformUtf16_register_allocator_helper(
    deephaven::dhcore::interop::PlatformUtf16::allocatorHelper_t allocatorHelper);
}  // extern "C"
