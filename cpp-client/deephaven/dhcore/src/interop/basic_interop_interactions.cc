#include "deephaven/dhcore/interop/basic_interop_interactions.h"
#include "deephaven/dhcore/utility/utility.h"

#include <string>

using deephaven::dhcore::interop::InteropBool;
using deephaven::dhcore::interop::PlatformUtf16;
using deephaven::dhcore::interop::StringHandle;
using deephaven::dhcore::interop::StringPool;
using deephaven::dhcore::interop::StringPoolHandle;
using deephaven::dhcore::interop::StringPoolBuilder;
using deephaven::dhcore::interop::PlatformUtf16;
using deephaven::dhcore::interop::Utf16Converter;
using deephaven::dhcore::utility::MakeReservedVector;

extern "C" {
void deephaven_dhcore_basicInteropInteractions_Add(int32_t a, int32_t b, int32_t *result) {
  *result = a + b;
}

void deephaven_dhcore_basicInteropInteractions_AddArrays(
    const int32_t *a, const int32_t *b, int32_t length, int32_t *result) {
  for (int32_t i = 0; i != length; ++i) {
    result[i] = a[i] + b[i];
  }
}

void deephaven_dhcore_basicInteropInteractions_Xor(InteropBool a, InteropBool b,
    InteropBool *result) {
  *result = InteropBool((bool)a ^ (bool)b);
}

void deephaven_dhcore_basicInteropInteractions_XorArrays(
    const InteropBool *a, const InteropBool *b, int32_t length, InteropBool *result) {
  for (int32_t i = 0; i != length; ++i) {
    result[i] = InteropBool((bool)a[i] ^ (bool)b[i]);
  }
}

void deephaven_dhcore_basicInteropInteractions_Concat(const char *a, const char *b,
    StringHandle *result_handle, StringPoolHandle *string_pool_handle) {
  StringPoolBuilder builder;
  auto text = std::string(a) + b;
  *result_handle = builder.Add(text);
  *string_pool_handle = builder.Build();
}

void deephaven_dhcore_basicInteropInteractions_ConcatArrays(const char **a, const char **b,
    int32_t length,
    deephaven::dhcore::interop::StringHandle *result_handles,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle) {
  StringPoolBuilder builder;
  for (int32_t i = 0; i != length; ++i) {
    auto text = std::string(a[i]) + b[i];
    result_handles[i] = builder.Add(text);
  }
  *string_pool_handle = builder.Build();
}

void deephaven_dhcore_basicInteropInteractions_BasicStruct(
    const BasicStructIn *data, int i_offset, const char16_t *s_append, BasicStructOut *result) {
  result->i = data->i + i_offset;

  // As a contrast to the above, we do the concat operation without converting back and
  // forth to UTF-8
  auto concatted_utf16 = std::u16string(data->s) + s_append;
  result->s = PlatformUtf16::Create(concatted_utf16);
}

void deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(
    const char16_t **data, int32_t length, const char16_t *to_append,
    const deephaven::dhcore::interop::PlatformUtf16 **result) {
  // This is the demonstration of an advanced technique. Since every call to PlaformUtf16::Create
  // has to cross the boundary back to native, we gather the values here so we can use a batch call
  auto values = MakeReservedVector<std::u16string>(length);
  for (int32_t i = 0; i != length; ++i) {
    values.push_back(std::u16string(data[i]) + to_append);
  }
  PlatformUtf16::CreateBulk(values.data(), values.size(), result);
}

void deephaven_dhcore_basicInteropInteractions_Less(int32_t a, int32_t b, InteropBool *result) {
  *result = InteropBool(a < b);
}

void deephaven_dhcore_basicInteropInteractions_Less_Array(const int32_t *a, const int32_t *b,
    int32_t length, InteropBool *results) {
  for (int32_t i = 0; i != length; ++i) {
    results[i] = InteropBool(a[i] < b[i]);
  }
}
}  // extern "C"
