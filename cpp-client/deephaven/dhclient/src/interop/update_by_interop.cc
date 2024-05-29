/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/interop/update_by_interop.h"

#include "deephaven/client/client.h"
#include "deephaven/client/client_options.h"
#include "deephaven/dhcore/interop/interop_util.h"

using deephaven::dhcore::interop::ErrorStatus;
using deephaven::dhcore::interop::InteropBool;
using deephaven::dhcore::interop::NativePtr;

using deephaven::client::ClientOptions;
using deephaven::client::UpdateByOperation;
using deephaven::client::update_by::DeltaControl;
using deephaven::client::update_by::OperationControl;
using deephaven::client::utility::DurationSpecifier;

extern "C" {
void deephaven_client_UpdateByOperation_dtor(
    NativePtr<UpdateByOperation> self) {
  delete self.Get();
}

void deephaven_client_update_by_cumSum(
    const char **cols, int32_t num_cols,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_cumProd(
    const char **cols, int32_t num_cols,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_cumMin(
    const char **cols, int32_t num_cols,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_cumMax(
    const char **cols, int32_t num_cols,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_forwardFill(
    const char **cols, int32_t num_cols,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_delta(
    const char **cols, int32_t num_cols,
    deephaven::client::update_by::DeltaControl delta_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emaTick(double decay_ticks,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emaTime(const char *timestamp_col,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> decay_time,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emsTick(double decay_ticks,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emsTime(const char *timestamp_col,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> decay_time,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emminTick(double decay_ticks,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emminTime(const char *timestamp_col,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> decay_time,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emmaxTick(double decay_ticks,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emmaxTime(const char *timestamp_col,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> decay_time,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emstdTick(double decay_ticks,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_emstdTime(const char *timestamp_col,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> decay_time,
    const char **cols, int32_t num_cols,
    const OperationControl *op_control,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingSumTick(
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingSumTime(const char *timestamp_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingGroupTick(
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingGroupTime(const char *timestamp_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingAvgTick(
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingAvgTime(const char *timestamp_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingMinTick(
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingMinTime(const char *timestamp_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingMaxTick(
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingMaxTime(const char *timestamp_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingProdTick(
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingProdTime(const char *timestamp_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingCountTick(
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingCountTime(const char *timestamp_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingStdTick(
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingStdTime(const char *timestamp_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingWavgTick(const char *weight_col,
    const char **cols, int32_t num_cols,
    int32_t rev_ticks, int32_t fwd_ticks,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
void deephaven_client_update_by_rollingWavgTime(const char *timestamp_col,
    const char *weight_col,
    const char **cols, int32_t num_cols,
    NativePtr<DurationSpecifier> rev_time,
    NativePtr<DurationSpecifier> fwd_time,
    NativePtr<UpdateByOperation> *result,
    ErrorStatus *status);
}  // extern "C"
