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
template<typename T>
class NativePtr {
public:
  NativePtr &operator=(T *target);

  T &operator*() { return *target_; }
  const T &operator*() const { return *target_; }
  T *operator->() { return target_; }
  const T *operator->() const { return target_; }

  T *get();
  const T *get() const;

private:
  T *target_ = nullptr;
};

using Utf16Converter = std::wstring_convert<std::codecvt_utf8_utf16<char16_t>, char16_t>;

/**
 * This is a special opaque class that we use to help us remember to return the right
 * kind of string to the platform (e.g. Windows). We have standardized on UTF16, which
 * means the C++ type we need to return is const char16_t*. However, the P/Invoke rules require
 * that that char16_t* be allocated in a specific way  (on Windows this is via CoTaskMemAlloc,
 * but on Linux it is malloc I think).
 *
 * We adopt the convention that for such strings we return a const PlatformUtf16* (which in
 * fact has been shamelessly cast from const char16_t*). For the allocation issue, rather
 * than calling CoTaskMemAlloc or malloc in a platform-specific way, we borrow a trick from
 * SWIG. We make the calling code install an "allocator" callback that itself doesn't do much,
 * but because the platform has to follow the marshalling rules, the marshalling will do
 * the string allocation for us.
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
 * As you can see it doesn't do much, but it arranges things so that the marshalling code
 * can do some string copying:
 *
 * The delegate that points to this method is very special:
 *
 * [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
 * public delegate void PlatformAllocatorHelperDelegate(
 *   [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] inStrings,
 *   [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] outStrings,
 *   int count);
 *
 * At application startup time this delegate is passed to the C++ side and turned into a
 * PlatformUtf16::platformAllocatorHelper_t, and stored as a static member of our class
 * (hidden behind a static accessor function for reasons).
 */
//
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

  // Can't actually create an instance of this class.
  PlatformUtf16() = delete;

  /**
   * Convenience function
   */
  static const PlatformUtf16 *Create(std::u16string_view s) {
    const char16_t *in_item = s.data();
    const PlatformUtf16 *out_item;
    Create(&in_item, &out_item, 1);
    return out_item;
  }

  static void Create(const char16_t **in_items, const PlatformUtf16 **out_items, int32_t count) {
    // Shameless cast
    const auto **out_items_to_use = reinterpret_cast<const char16_t**>(out_items);
    AllocatorHelper()(in_items, out_items_to_use, count);
  }
};

/**
 * This is a special class that we use to help us remember to return the right
 * kind of string to the platform (e.g. Windows). We have standardized on UTF16, which
 * means the C++ type we need to return is const char16_t*. However, the P/Invoke rules require
 * that that char16_t* be allocated in a specific way  (on Windows this is via CoTaskMemAlloc,
 * but on Linux it is malloc I think).
 *
 * We adopt the following convention for returning strings to Windows. On the Windows side
 * we have a struct Platform16
 * [TODO]
 * convention that for such strings we return a PlatformUtf16* (which in
 * fact has been shamelessly cast from const char16_t*). For the allocation issue, rather
 * than calling CoTaskMemAlloc or malloc in a platform-specific way, we borrow a trick from
 * SWIG. We make the calling code install an "allocator" callback that itself doesn't do much,
 * but because the platform has to follow the marshalling rules, the marshalling will do
 * the string allocation for us.
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
 * As you can see it doesn't do much, but it arranges things so that the marshalling code
 * can do some string copying:
 *
 * The delegate that points to this method is very special:
 *
 * [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
 * public delegate void PlatformAllocatorHelperDelegate(
 *   [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] inStrings,
 *   [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] string[] outStrings,
 *   int count);
 *
 * At application startup time this delegate is passed to the C++ side and turned into a
 * PlatformUtf16::platformAllocatorHelper_t, and stored as a static member of our class
 * (hidden behind a static accessor function for reasons).
 */
//
class PlatformUtf16v2 {
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

  static PlatformUtf16v2 Create(std::string_view item);
  static PlatformUtf16v2 Create(std::u16string_view  item);
  static void CreateBulk(const std::string *strings, size_t num_strings, PlatformUtf16v2 *results);
  static void CreateBulk(const std::u16string *strings, size_t num_strings, PlatformUtf16v2 *results);

  //static void Create(const char16_t **in_items, PlatformUtf16v2 *out_items, int32_t count);

  void Reset() {
    data_ = nullptr;
  }

private:
  explicit PlatformUtf16v2(const char16_t *data) : data_(data) {}
  const char16_t *data_ = nullptr;
};

class ErrorStatus {
public:
  template<typename T>
  void Run(const T &callback) {
    text_.Reset();
    try {
      callback();
    } catch (const std::exception &e) {
      text_ = PlatformUtf16v2::Create(e.what());
    } catch (...) {
      text_ = PlatformUtf16v2::Create("Unknown exception");
    }
  }

private:
  PlatformUtf16v2 text_;
};

class WrappedException {
public:
  explicit WrappedException(std::string what) : what_(std::move(what)) {}

  [[nodiscard]]
  const std::string &What() const { return what_; }

  template<typename T>
  static WrappedException *Run(const T &callback) {
    try {
      callback();
      return nullptr;
    } catch (const std::exception &e) {
      return new WrappedException(e.what());
    } catch (...) {
      return new WrappedException("Unknown exception");
    }
  }

private:
  std::string what_;
};

class NativeError {
public:
  NativeError() = default;
  explicit NativeError(std::string_view s) {
    std::cerr << "trying to make a " << s << '\n';
    auto utf16 = Utf16Converter().from_bytes(s.data());
    text_ = PlatformUtf16::Create(utf16);
    std::cerr << "constructor returning ok\n";
  }

  template<typename T>
  static NativeError Run(const T &callback) {
    try {
      callback();
      std::cerr << "callback successful\n";
      return NativeError("Q");
    } catch (const std::exception &e) {
      return NativeError(e.what());
    } catch (...) {
      return NativeError("Unknown exception");
    }
  }

private:
  const PlatformUtf16 *text_;
};

template<typename T>
struct ResultOrError {
  template<typename U>
  void SetResult(const U &callback) {
    try {
      result_ = callback();
      error_ = nullptr;
    } catch (const std::exception &e) {
      result_ = nullptr;
      error_ = new WrappedException(e.what());
    } catch (...) {
      std::cout << "Zamboni time!!!!\n";
      result_ = nullptr;
      error_ = new WrappedException("Unknown exception");
    }
  }

  T *result_ = nullptr;
  WrappedException *error_ = nullptr;
};
}  // namespace deephaven::dhcore::interop

extern "C" {
void deephaven_dhcore_interop_PlatformUtf16_register_allocator_helper(
    deephaven::dhcore::interop::PlatformUtf16::allocatorHelper_t allocatorHelper);

void deephaven_client_WrappedException_dtor(
    deephaven::dhcore::interop::WrappedException *self);
const deephaven::dhcore::interop::PlatformUtf16 *deephaven_client_WrappedException_What(
    deephaven::dhcore::interop::WrappedException *self);
}  // extern "C"
