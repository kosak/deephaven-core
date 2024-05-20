#pragma once

#include <cstdint>
#include "deephaven/dhcore/interop/interop_util.h"

namespace deephaven::dhcore::interop::testapi {
struct BasicStruct {
  BasicStruct(int i, double d) : i_(i), d_(d) {}

  [[nodiscard]]
  BasicStruct Add(const BasicStruct &other) const {
    return BasicStruct(i_ + other.i_, d_ + other.d_);
  }

  int i_;
  double d_;
};

struct NestedStruct {
  NestedStruct(const BasicStruct &a, const BasicStruct &b) : a_(a), b_(b) {}

  [[nodiscard]]
  NestedStruct Add(const NestedStruct &other) const {
    return NestedStruct(a_.Add(other.a_), b_.Add(other.b_));
  }

  BasicStruct a_;
  BasicStruct b_;
};
}

extern "C" {
void deephaven_dhcore_interop_testapi_BasicInteropInteractions_Add(
    int32_t a, int32_t b, int32_t *result);
void deephaven_dhcore_interop_testapi_BasicInteropInteractions_AddArrays(
    const int32_t *a, const int32_t *b, int32_t length, int32_t *result);
void deephaven_dhcore_interop_testapi_BasicInteropInteractions_Xor(
    deephaven::dhcore::interop::InteropBool a,
    deephaven::dhcore::interop::InteropBool b,
    deephaven::dhcore::interop::InteropBool *result);
void deephaven_dhcore_interop_testapi_BasicInteropInteractions_XorArrays(
    const deephaven::dhcore::interop::InteropBool *a,
    const deephaven::dhcore::interop::InteropBool *b,
    int32_t length,
    deephaven::dhcore::interop::InteropBool *result);
void deephaven_dhcore_interop_testapi_BasicInteropInteractions_Concat(
    const char *a, const char *b,
    deephaven::dhcore::interop::StringHandle *result_handle,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle);
void deephaven_dhcore_interop_testapi_BasicInteropInteractions_ConcatArrays(const char **a, const char **b,
    int32_t length,
    deephaven::dhcore::interop::StringHandle *result_handles,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle);


void deephaven_dhcore_interop_testapi_BasicInteropInteractions_AddBasicStruct(
    const deephaven::dhcore::interop::testapi::BasicStruct *a,
    const deephaven::dhcore::interop::testapi::BasicStruct *b,
    deephaven::dhcore::interop::testapi::BasicStruct *result);

void deephaven_dhcore_interop_testapi_BasicInteropInteractions_AddBasicStructArrays(
    const deephaven::dhcore::interop::testapi::BasicStruct *a,
    const deephaven::dhcore::interop::testapi::BasicStruct *b,
    int32_t length,
    deephaven::dhcore::interop::testapi::BasicStruct *result);


void deephaven_dhcore_interop_testapi_BasicInteropInteractions_AddNestedStruct(
    const deephaven::dhcore::interop::testapi::NestedStruct *a,
    const deephaven::dhcore::interop::testapi::NestedStruct *b,
    deephaven::dhcore::interop::testapi::NestedStruct *result);

void deephaven_dhcore_interop_testapi_BasicInteropInteractions_AddNestedStructArrays(
    const deephaven::dhcore::interop::testapi::NestedStruct *a,
    const deephaven::dhcore::interop::testapi::NestedStruct *b,
    int32_t length,
    deephaven::dhcore::interop::testapi::NestedStruct *result);

void deephaven_dhcore_interop_testapi_BasicInteropInteractions_SetErrorIfLessThan(
    int32_t a, int32_t b,
    deephaven::dhcore::interop::ErrorStatus *error_status);
}  // extern "C"
