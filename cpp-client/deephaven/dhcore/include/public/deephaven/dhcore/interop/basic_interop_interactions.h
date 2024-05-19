#pragma once

#include <cstdint>
#include "deephaven/dhcore/interop/interop_util.h"

extern "C" {
void deephaven_dhcore_basicInteropInteractions_Add(int32_t a, int32_t b, int32_t *result);
void deephaven_dhcore_basicInteropInteractions_Concat(const char16_t *a, const char16_t *b,
    const deephaven::dhcore::interop::PlatformUtf16 **result);

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

void deephaven_dhcore_basicInteropInteractions_ArrayRunningSum(
    const int32_t *data, int32_t length, int32_t *result);

void deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(
    const char16_t **data, int32_t length, const char16_t *to_append,
    const deephaven::dhcore::interop::PlatformUtf16 **result);

void deephaven_dhcore_basicInteropInteractions_Less(int32_t a, int32_t b,
    deephaven::dhcore::interop::InteropBool *result);
void deephaven_dhcore_basicInteropInteractions_Less_Array(const int32_t *a, const int32_t *b,
    int32_t length, deephaven::dhcore::interop::InteropBool *results);
}  // extern "C"
