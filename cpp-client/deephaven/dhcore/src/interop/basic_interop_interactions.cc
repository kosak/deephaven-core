#include "deephaven/dhcore/interop/basic_interop_interactions.h"

#include <string>

using deephaven::dhcore::interop::PlatformUtf16;
using deephaven::dhcore::interop::Utf16Converter;

extern "C" {
void deephaven_dhcore_basicInteropInteractions_Add(int32_t a, int32_t b, int32_t *result) {
  *result = a + b;
}

void deephaven_dhcore_basicInteropInteractions_Concat(const char16_t *a, const char16_t *b,
    const PlatformUtf16 **result) {
  // This converts to UTF-8, does a concat, and then converts back to UTF-16.
  // You could have just stayed in UTF-16 the whole time.
  Utf16Converter uc;
  auto a_utf8 = uc.to_bytes(a);
  auto b_utf8 = uc.to_bytes(b);
  auto concatted = a_utf8 + b_utf8;
  auto concatted_utf16 = uc.from_bytes(concatted);
  *result = PlatformUtf16::Create(concatted_utf16);
}

void deephaven_dhcore_basicInteropInteractions_BasicStruct(
    const BasicStructIn *data, int i_offset, const char16_t *s_append, BasicStructOut *result) {
  result->i = data->i + i_offset;

  // As a contrast to the above, we do the concat operation without converting back and
  // forth to UTF-8
  auto concatted_utf16 = std::u16string(data->s) + s_append;
  result->s = PlatformUtf16::Create(concatted_utf16);
}

void deephaven_dhcore_basicInteropInteractions_ArrayRunningSum(
    const int32_t *data, int32_t length, int32_t *result) {
  int32_t sum = 0;
  for (int32_t i = 0; i != length; ++i) {
    sum += data[i];
    result[i] = sum;
  }
}

void deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(
    const char16_t **data, int32_t length, const char16_t *to_append,
    deephaven::dhcore::interop::PlatformUtf16 **result);
}  // extern "C"
