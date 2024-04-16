/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/dhcore/interop/interop_util.h"
#include "deephaven/dhcore/utility/utility.h"

#include <vector>

using deephaven::dhcore::interop::Utf16Converter;
using deephaven::dhcore::utility::MakeReservedVector;

namespace deephaven::dhcore::interop {
PlatformUtf16::allocatorHelper_t &PlatformUtf16::AllocatorHelper() {
  // We make this a function rather than a global because it plays more nicely with
  // CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS.
  static allocatorHelper_t allocator_helper;
  return allocator_helper;
}

PlatformUtf16 PlatformUtf16::Create(std::string_view s) {
  Utf16Converter c;
  auto u16_text = c.from_bytes(s.data());
  return Create(u16_text);
}

PlatformUtf16 PlatformUtf16::Create(std::u16string_view s) {
  const char16_t *in_item = s.data();
  const char16_t *out_item;
  AllocatorHelper()(&in_item, &out_item, 1);
  return PlatformUtf16(out_item);
}

void PlatformUtf16::CreateBulk(const std::string *strings, size_t num_strings,
    PlatformUtf16 *results) {
  Utf16Converter c;
  auto u16_strings = MakeReservedVector<std::u16string>(num_strings);
  for (size_t i = 0; i != num_strings; ++i) {
    u16_strings.push_back(c.from_bytes(strings[i].data()));
  }
  CreateBulk(u16_strings.data(), u16_strings.size(), results);
}

void PlatformUtf16::CreateBulk(const std::u16string *strings, size_t num_strings,
    PlatformUtf16 *results) {
  auto u16_ptrs = MakeReservedVector<const char16_t*>(num_strings);
  for (size_t i = 0; i != num_strings; ++i) {
    u16_ptrs.push_back(strings[i].data());
  }
  std::vector<const char16_t*> result_ptrs(num_strings);
  AllocatorHelper()(u16_ptrs.data(), result_ptrs.data(), num_strings);
  for (size_t i = 0; i != num_strings; ++i) {
    results[i] = PlatformUtf16(result_ptrs[i]);
  }
}
}  // namespace deephaven::dhcore::interop

extern "C" {
void deephaven_dhcore_interop_PlatformUtf16_register_allocator_helper(
    deephaven::dhcore::interop::PlatformUtf16::allocatorHelper_t allocator_helper) {
  deephaven::dhcore::interop::PlatformUtf16::AllocatorHelper() = allocator_helper;
}
}  // extern "C"
