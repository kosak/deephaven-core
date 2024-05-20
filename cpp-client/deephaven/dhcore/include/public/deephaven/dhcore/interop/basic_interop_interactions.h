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

struct BasicStruct {
  BasicStruct(int i, double d) : i_(i), d_(d) {}

  BasicStruct Add(const BasicStruct &other) const;

  int i_;
  double d_;
};

void deephaven_dhcore_basicInteropInteractions_AddBasicStruct(
    const BasicStruct *a, const BasicStruct *b, BasicStruct *result);

void deephaven_dhcore_basicInteropInteractions_AddBasicStructArrays(
    const BasicStruct *a, const BasicStruct *b, int32_t length, BasicStruct *result);

struct NestedStruct {
  NestedStruct(const BasicStruct &a, const BasicStruct &b) : a_(a), b_(b) {}

  NestedStruct Add(const NestedStruct &other) const;

  BasicStruct a_;
  BasicStruct b_;
};

void deephaven_dhcore_basicInteropInteractions_AddNestedStruct(
    const NestedStruct *a, const NestedStruct *b, NestedStruct *result);
}  // extern "C"
