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

BasicStruct BasicStruct::Add(const BasicStruct &other) const {
  return BasicStruct(i_ + other.i_, d_ + other.d_);
}

void deephaven_dhcore_basicInteropInteractions_AddBasicStruct(
    const BasicStruct *a, const BasicStruct *b, BasicStruct *result) {
  *result = a->Add(*b);
}

void deephaven_dhcore_basicInteropInteractions_AddBasicStructArrays(
    const BasicStruct *a, const BasicStruct *b, int32_t length, BasicStruct *result) {
  for (int32_t i = 0; i != length; ++i) {
    result[i] = a[i].Add(b[i]);
  }
}

NestedStruct NestedStruct::Add(const NestedStruct &other) const {
  return NestedStruct(a_.Add(other.a_), b_.Add(other.b_));
}

void deephaven_dhcore_basicInteropInteractions_AddNestedStruct(
    const NestedStruct *a, const NestedStruct *b, NestedStruct *result) {
  *result = a->Add(*b);
}
}  // extern "C"
