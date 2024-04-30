#pragma once

#include <cstdint>
#include "deephaven/dhcore/interop/interop_util.h"

extern "C" {
void deephaven_dhcore_basicInteropInteractions_Add(int32_t a, int32_t b, int32_t *result);
void deephaven_dhcore_basicInteropInteractions_Concat(const char16_t *a, const char16_t *b,
    deephaven::dhcore::interop::PlatformUtf16 *result);

struct BasicStructIn {
  int i;
  const char16_t *s;
};

struct BasicStructOut {
  int i;
  const deephaven::dhcore::interop::PlatformUtf16 *s;
};

void deephaven_dhcore_basicInteropInteractions_BasicStruct(
    const BasicStructIn *data, int iOffset, const char16_t *sAppend, BasicStructOut *result);

void deephaven_dhcore_basicInteropInteractions_ArrayRunningSum(
    const int32_t *data, int32_t length, int32_t *result);

void deephaven_dhcore_basicInteropInteractions_ArrayElementConcat(
    const char16_t **data, int32_t length, const char16_t *toAppend,
    deephaven::dhcore::interop::PlatformUtf16 **result);
}  // extern "C"
