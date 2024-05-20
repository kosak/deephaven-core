/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/dhcore/interop/interop_util.h"
#include "deephaven/dhcore/utility/utility.h"

#include <vector>

using deephaven::dhcore::interop::Utf16Converter;
using deephaven::dhcore::utility::MakeReservedVector;

namespace deephaven::dhcore::interop {
namespace {
void DefaultAllocatorHelper(const char16_t **/*in_items*/, const PlatformUtf16 **out_items,
    int32_t count) {
  std::cerr << "ERROR: AllocatorHelper was never set\n";
  for (int32_t i = 0; i != count; ++i) {
    out_items[i] = nullptr;
  }
}
}  // namespace

PlatformUtf16::allocatorHelper_t &PlatformUtf16::AllocatorHelper() {
  // We make this a function rather than a global because it plays more nicely with
  // CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS.
  static allocatorHelper_t allocator_helper = &DefaultAllocatorHelper;
  return allocator_helper;
}

const PlatformUtf16 *PlatformUtf16::Create(std::string_view s) {
  Utf16Converter c;
  auto u16_text = c.from_bytes(s.data());
  return Create(u16_text);
}

const PlatformUtf16 *PlatformUtf16::Create(std::u16string_view s) {
  const char16_t *in_item = s.data();
  const PlatformUtf16 *out_item;
  AllocatorHelper()(&in_item, &out_item, 1);
  return out_item;
}

void PlatformUtf16::CreateBulk(const std::string *strings, size_t num_strings,
    const PlatformUtf16 **results) {
  Utf16Converter c;
  auto u16_strings = MakeReservedVector<std::u16string>(num_strings);
  for (size_t i = 0; i != num_strings; ++i) {
    u16_strings.push_back(c.from_bytes(strings[i].data()));
  }
  CreateBulk(u16_strings.data(), u16_strings.size(), results);
}

void PlatformUtf16::CreateBulk(const std::u16string *strings, size_t num_strings,
    const PlatformUtf16 **results) {
  auto u16_ptrs = MakeReservedVector<const char16_t*>(num_strings);
  for (size_t i = 0; i != num_strings; ++i) {
    u16_ptrs.push_back(strings[i].data());
  }
  AllocatorHelper()(u16_ptrs.data(), results, static_cast<int32_t>(num_strings));
}

void PlatformUtf16::CreateBulk(const char16_t **strings, size_t num_strings,
    const deephaven::dhcore::interop::PlatformUtf16 **results) {
  AllocatorHelper()(strings, results, static_cast<int32_t>(num_strings));
}

StringPool::StringPool(std::vector<uint8_t> bytes, std::vector<int32_t> ends) :
   bytes_(std::move(bytes)), ends_(std::move(ends)) {}
StringPool::~StringPool() = default;

void StringPool::Export(uint8_t *bytes, int32_t bytes_length,
    int32_t *ends, int32_t ends_length) {
  std::copy(bytes_.begin(), bytes_.end(), bytes);
  std::copy(ends_.begin(), ends_.end(), ends);
}

StringPoolBuilder::StringPoolBuilder() = default;
StringPoolBuilder::~StringPoolBuilder() = default;

StringHandle StringPoolBuilder::Add(std::string_view sv) {
  StringHandle result(static_cast<int32_t>(ends_.size()));
  bytes_.insert(bytes_.end(), sv.begin(), sv.end());
  ends_.push_back(static_cast<int32_t>(bytes_.size()));
  return result;
}

StringPoolHandle StringPoolBuilder::Build() {
  auto num_bytes = bytes_.size();
  auto num_strings = ends_.size();
  auto *sp = new StringPool(std::move(bytes_), std::move(ends_));
  return StringPoolHandle(sp, num_bytes, num_strings);
}
}  // namespace deephaven::dhcore::interop

extern "C" {
void deephaven_dhcore_interop_PlatformUtf16_register_allocator_helper(
    deephaven::dhcore::interop::PlatformUtf16::allocatorHelper_t allocator_helper) {
  deephaven::dhcore::interop::PlatformUtf16::AllocatorHelper() = allocator_helper;
}

void deephaven_dhcore_interop_StringPool_ExportAndDestroy(
    deephaven::dhcore::interop::StringPool *string_pool,
    uint8_t *bytes, int32_t bytes_length,
    int32_t *ends, int32_t ends_length) {
  string_pool->Export(bytes, bytes_length, ends, ends_length);
  delete string_pool;
}
}  // extern "C"
