/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include <cstdint>
#include "deephaven/client/client.h"
#include "deephaven/client/client_options.h"
#include "deephaven/client/update_by.h"
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/dhcore/interop/interop_util.h"


namespace deephaven::client::interop {
void deephaven_client_UpdateByOperation_dtor(
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> self);
void deephaven_client_update_by_cumSum(
    const char **cols, int32_t num_cols,
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> *result,
    deephaven::dhcore::interop::ErrorStatus *status);
void deephaven_client_update_by_cumProd(
    const char **cols, int32_t num_cols,
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> *result,
    deephaven::dhcore::interop::ErrorStatus *status);
void deephaven_client_update_by_cumMin(
    const char **cols, int32_t num_cols,
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> *result,
    deephaven::dhcore::interop::ErrorStatus *status);
void deephaven_client_update_by_cumMax(
    const char **cols, int32_t num_cols,
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> *result,
    deephaven::dhcore::interop::ErrorStatus *status);
void deephaven_client_update_by_forwardFill(
    const char **cols, int32_t num_cols,
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> *result,
    deephaven::dhcore::interop::ErrorStatus *status);
void deephaven_client_update_by_delta(
    const char **cols, int32_t num_cols,
    deephaven::client::update_by::DeltaControl delta_control,
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> *result,
    deephaven::dhcore::interop::ErrorStatus *status);
void deephaven_client_update_by_emaTick(double decay_ticks,
    const char **cols, int32_t num_cols,
    const deephaven::client::update_by::OperationControl *op_control,
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> *result,
    deephaven::dhcore::interop::ErrorStatus *status);
void deephaven_client_update_by_emaTime(const char *timestamp_col,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> decay_time,
    const char **cols, int32_t num_cols,
    const deephaven::client::update_by::OperationControl *op_control,
    deephaven::dhcore::interop::NativePtr<UpdateByOperation> *result,
    deephaven::dhcore::interop::ErrorStatus *status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_emsTick(double decayTicks,
      string[] cols, Int32 numCols, ref OperationControl opControl,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_emsTime(string timestampCol,
      NativePtr<NativeDurationSpecifier> decayTime, string[] cols, Int32 numCols,
      ref OperationControl opControl, out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_emminTick(double decayTicks,
      string[] cols, Int32 numCols, ref OperationControl opControl,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_emminTime(string timestampCol,
      NativePtr<NativeDurationSpecifier> decayTime, string[] cols, Int32 numCols,
      ref OperationControl opControl, out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_emmaxTick(double decayTicks,
      string[] cols, Int32 numCols, ref OperationControl opControl,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_emmaxTime(string timestampCol,
      NativePtr<NativeDurationSpecifier> decayTime, string[] cols, Int32 numCols,
      ref OperationControl opControl, out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_emstdTick(double decayTicks,
      string[] cols, Int32 numCols, ref OperationControl opControl,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_emstdTime(string timestampCol,
      NativePtr<NativeDurationSpecifier> decayTime, string[] cols, Int32 numCols,
      ref OperationControl opControl, out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingSumTick(
      string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingSumTime(string timestampCol,
      string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingGroupTick(
      string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingGroupTime(string timestampCol,
      string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingAvgTick(
      string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingAvgTime(string timestampCol,
      string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingMinTick(
      string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingMinTime(string timestampCol,
      string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingMaxTick(
      string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingMaxTime(string timestampCol,
      string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingProdTick(
      string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingProdTime(string timestampCol,
      string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingCountTick(
      string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingCountTime(string timestampCol,
      string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingStdTick(
      string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingStdTime(string timestampCol,
      string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingWavgTick(
      string weightCol, string[] cols, Int32 numCols, Int32 revTicks, Int32 fwdTicks,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
  [LibraryImport(LibraryPaths.Dhclient, StringMarshalling = StringMarshalling.Utf8)]
public static partial void deephaven_client_update_by_rollingWavgTime(string timestampCol,
      string weightCol, string[] cols, Int32 numCols, NativePtr<NativeDurationSpecifier> revTime,
      NativePtr<NativeDurationSpecifier> fwdTime,
      out NativePtr<NativeUpdateByOperation> result, out ErrorStatus status);
}


}  // namespace deephaven::client::interop

extern "C" {
}  // extern "C"
