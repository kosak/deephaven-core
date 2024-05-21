/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#pragma once
#include <cstdint>
#include <exception>
#include <iostream>
#include <locale>
#include <string>
#include <string_view>
#include <vector>

namespace deephaven::dhcore::interop {
/**
 * This class simply wraps a pointer. It is meant to mirror a similar struct on the C++ side, namely
 *
 * [StructLayout(LayoutKind.Sequential)]
 * public struct NativePtr<T> {
 *   public IntPtr ptr;
 * }
 *
 * Even though this struct just wraps a bare IntPtr, making the enclosing struct generic gives
 * us a little more typechecking on the C# side.
 */
template<typename T>
struct NativePtr {
  explicit NativePtr(T *ptr) : ptr_(ptr) {}

  [[nodiscard]]
  T *Get() const { return ptr_; }

  [[nodiscard]]
  operator T*() const { return ptr_; }

  [[nodiscard]]
  T *operator ->() const { return ptr_; }

  void Reset(T *new_ptr) { ptr_ = new_ptr; }

private:
  T *ptr_ = nullptr;
};

class StringPool {
public:
  static int32_t ExportAndDestroy(
      StringPool *self,
      uint8_t *bytes, int32_t bytes_length,
      int32_t *ends, int32_t ends_length);

  StringPool(std::vector<uint8_t> bytes, std::vector<int32_t> ends);
  StringPool(const StringPool &other) = delete;
  StringPool &operator=(const StringPool &other) = delete;
  ~StringPool();

private:
  std::vector<uint8_t> bytes_;
  std::vector<int32_t> ends_;
};

struct StringHandle {
  explicit StringHandle(int32_t index) : index_(index) {}

  int32_t index_ = 0;
};

struct StringPoolHandle {
  StringPoolHandle() = default;
  StringPoolHandle(StringPool *string_pool, int32_t num_bytes, int32_t num_strings) :
      stringPool_(string_pool), numBytes_(num_bytes), numStrings_(num_strings) {}

  StringPool *stringPool_ = nullptr;
  int32_t numBytes_ = 0;
  int32_t numStrings_ = 0;
};

class StringPoolBuilder {
public:
  StringPoolBuilder();
  StringPoolBuilder(const StringPoolBuilder &other) = delete;
  StringPoolBuilder &operator=(const StringPoolBuilder &other) = delete;
  ~StringPoolBuilder();

  [[nodiscard]]
  StringHandle Add(std::string_view sv);
  [[nodiscard]]
  StringPoolHandle Build();

private:
  std::vector<uint8_t> bytes_;
  std::vector<int32_t> ends_;
};

/**
 * Not safe to pass .NET bool to interop: use int8_t (aka .NET sbyte) instead.
 */
class InteropBool {
public:
  explicit InteropBool(bool value) : value_(value ? 1 : 0) {}

  explicit operator bool() const { return value_ != 0; }

private:
  int8_t value_ = 0;
};

class ErrorStatusNew {
public:
  /**
   * This is used to wrap caller code in a lambda so that we can automatically set the error
   * status if the lambda throws an exception.
   */
  template<typename T>
  void Run(const T &callback) {
    StringPoolBuilder builder;
    try {
      // Sanity check for this method's callers to make sure they're not returning a value
      // which would be a programming mistake because it is ignored here.
      static_assert(std::is_same_v<decltype(callback()), void>);
      callback();
    } catch (const std::exception &e) {
      stringHandle_ = builder.Add(e.what());
    } catch (...) {
      stringHandle_ = builder.Add("Unknown exception");
    }
    stringPoolHandle_ = builder.Build();
  }

private:
  StringHandle stringHandle_;
  StringPoolHandle stringPoolHandle_;
};
}  // namespace deephaven::dhcore::interop

extern "C" {
int32_t deephaven_dhcore_interop_StringPool_ExportAndDestroy(
    deephaven::dhcore::interop::NativePtr<deephaven::dhcore::interop::StringPool> string_pool,
    uint8_t *bytes, int32_t bytes_length,
    int32_t *ends, int32_t ends_length);
}  // extern "C"
