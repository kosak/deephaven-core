#pragma once

#include <cstdint>
#include "deephaven/dhcore/interop/interop_util.h"

extern "C" {
void deephaven_dhcore_basicInteropInteractions_Add(int32_t a, int32_t b, int32_t *result);
void deephaven_dhcore_basicInteropInteractions_AddArrays(
    const int32_t *a, const int32_t *b, int32_t length, int32_t *result);
void deephaven_dhcore_basicInteropInteractions_Xor(
    deephaven::dhcore::interop::InteropBool a,
    deephaven::dhcore::interop::InteropBool b,
    deephaven::dhcore::interop::InteropBool *result);
void deephaven_dhcore_basicInteropInteractions_XorArrays(
    const deephaven::dhcore::interop::InteropBool *a,
    const deephaven::dhcore::interop::InteropBool *b,
    int32_t length,
    deephaven::dhcore::interop::InteropBool *result);
void deephaven_dhcore_basicInteropInteractions_Concat(const char *a, const char *b,
    deephaven::dhcore::interop::StringHandle *result_handle,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle);
void deephaven_dhcore_basicInteropInteractions_ConcatArrays(const char **a, const char **b,
    int32_t length,
    deephaven::dhcore::interop::StringHandle *result_handles,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle);

struct BasicStructIn {
  int i;
  const char16_t *s;
};

struct BasicStructOut {
  int i;
  const deephaven::dhcore::interop::PlatformUtf16 *s;
};

void deephaven_dhcore_basicInteropInteractions_BasicStruct(
    const BasicStructIn *data, int i_offset, const char16_t *s_append, BasicStructOut *result);

void deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(
    const char16_t **data, int32_t length, const char16_t *to_append,
    const deephaven::dhcore::interop::PlatformUtf16 **result);

void deephaven_dhcore_basicInteropInteractions_Less(int32_t a, int32_t b,
    deephaven::dhcore::interop::InteropBool *result);
void deephaven_dhcore_basicInteropInteractions_Less_Array(const int32_t *a, const int32_t *b,
    int32_t length, deephaven::dhcore::interop::InteropBool *results);
}  // extern "C"
