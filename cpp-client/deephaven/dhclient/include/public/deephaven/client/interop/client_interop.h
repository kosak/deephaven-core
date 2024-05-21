/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include <cstdint>
#include "deephaven/client/client.h"
#include "deephaven/client/client_options.h"
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/dhcore/interop/interop_util.h"


namespace deephaven::client::interop {
/**
 * A thin wrapper about std::shared_ptr<ArrowTable>. Not really necessary but
 * makes it easier for humans to map to the corresponding class on the C# side.
 */
struct ArrowTable {
public:
  explicit ArrowTable(std::shared_ptr<arrow::Table> table) : table_(std::move(table)) {}
  std::shared_ptr<arrow::Table> table_;
};

/**
 * This class exists so we don't get confused about what we are passing
 * back and forth to .NET. Basically, like any other object, we need to
 * heap-allocate this object and pass an opaque pointer back and forth
 * to .NET. The fact that this object's only member is a shared pointer
 * is irrelevant in terms of what we need to do.
 */
struct ClientTableSpWrapper {
  using ClientTable = deephaven::dhcore::clienttable::ClientTable;
public:
  explicit ClientTableSpWrapper(std::shared_ptr<ClientTable> table) : table_(std::move(table)) {}
  std::shared_ptr<ClientTable> table_;
};
}  // namespace deephaven::client::interop {

extern "C" {
void deephaven_client_TableHandleManager_dtor(deephaven::client::TableHandleManager *self);

void deephaven_client_TableHandleManager_EmptyTable(const deephaven::client::TableHandleManager *self,
    int64_t size,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandleManager_FetchTable(const deephaven::client::TableHandleManager *self,
    const char16_t *table_name,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandleManager_TimeTable(const deephaven::client::TableHandleManager *self,
    const deephaven::client::utility::DurationSpecifier *period,
    const deephaven::client::utility::TimePointSpecifier *start_time,
    deephaven::dhcore::interop::InteropBool blink_table,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandleManager_InputTable(const deephaven::client::TableHandleManager *self,
    const deephaven::client::TableHandle *initial_table, const char16_t **key_columns,
    int64_t num_key_columns,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandleManager_RunScript(const deephaven::client::TableHandleManager *self,
    const char16_t *code,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_Client_Connect(const char *target,
    deephaven::dhcore::interop::NativePtr<deephaven::client::ClientOptions> options,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Client> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_Client_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::Client> self);

void deephaven_client_Client_Close(
    deephaven::dhcore::interop::NativePtr<deephaven::client::Client> self,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_Client_GetManager(
    deephaven::dhcore::interop::NativePtr<deephaven::client::Client> self,
    deephaven::dhcore::interop::NativePtr<deephaven::client::TableHandleManager> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_TableHandle_GetAttributes(deephaven::client::TableHandle *self,
    int32_t *num_columns, int64_t *num_rows,
    deephaven::dhcore::interop::InteropBool *is_static,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_GetSchema(deephaven::client::TableHandle *self,
    int32_t num_columns,
    const deephaven::dhcore::interop::PlatformUtf16 **columns, int32_t *column_types,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::TableHandle> self);

void deephaven_client_TableHandle_GetManager(deephaven::client::TableHandle *self,
    deephaven::client::TableHandleManager **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_Where(deephaven::client::TableHandle *self,
    const char16_t *condition,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_Select(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_SelectDistinct(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_View(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_DropColumns(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_Update(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_LazyUpdate(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_LastBy(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_Head(deephaven::client::TableHandle *self,
    int64_t num_rows,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_Tail(deephaven::client::TableHandle *self,
    int64_t num_rows,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_WhereIn(deephaven::client::TableHandle *self,
    deephaven::client::TableHandle *filter_table,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_AddTable(deephaven::client::TableHandle *self,
    deephaven::client::TableHandle *table_to_add,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_RemoveTable(deephaven::client::TableHandle *self,
    deephaven::client::TableHandle *table_to_remove,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_By(deephaven::client::TableHandle *self,
    const deephaven::client::AggregateCombo *aggregate_combo,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_BindToVariable(deephaven::client::TableHandle *self,
    const char16_t *variable,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_ToClientTable(
    deephaven::client::TableHandle *self,
    deephaven::client::interop::ClientTableSpWrapper **client_table,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_ToString(
    deephaven::client::TableHandle *self,
    int32_t want_headers,
    const deephaven::dhcore::interop::PlatformUtf16 **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_ToArrowTable(deephaven::client::TableHandle *self,
    deephaven::client::interop::ArrowTable **arrow_table,
    deephaven::dhcore::interop::ErrorStatus *status);

using NativeOnUpdate = void(deephaven::dhcore::ticking::TickingUpdate *ticking_update);
using NativeOnFailure = void(const char16_t *error);

void deephaven_client_TableHandle_Subscribe(deephaven::client::TableHandle *self,
    NativeOnUpdate *native_on_update, NativeOnFailure *native_on_failure,
    std::shared_ptr<deephaven::client::subscription::SubscriptionHandle> **native_subscription_handle,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_TableHandle_Unsubscribe(deephaven::client::TableHandle *self,
    std::shared_ptr<deephaven::client::subscription::SubscriptionHandle> *native_subscription_handle,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_ArrowTable_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ArrowTable> self);

void deephaven_client_ArrowTable_GetDimensions(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ArrowTable> self,
    int32_t *num_columns, int64_t *num_rows,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_ArrowTable_GetSchema(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ArrowTable> self,
    int32_t num_columns,
    deephaven::dhcore::interop::StringHandle *column_handles, int32_t *column_types,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_ClientTable_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self);

void deephaven_client_ClientTable_GetDimensions(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t *num_columns, int64_t *num_rows,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_ClientTable_Schema(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t num_columns,
    deephaven::dhcore::interop::StringHandle *column_handles,
    int32_t *column_types,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_ClientTableHelper_GetInt8Column(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, int8_t *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags, int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetInt16Column(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, int16_t *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags, int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetInt32Column(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, int32_t *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags, int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetInt64Column(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, int64_t *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags, int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetFloatColumn(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, float *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags, int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetDoubleColumn(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, double *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags,
    int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetCharAsInt16Column(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, int16_t *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags,
    int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetBooleanAsInteropBoolColumn(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, int8_t *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags, int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetStringColumn(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, deephaven::dhcore::interop::StringHandle *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags, int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_ClientTableHelper_GetDateTimeAsInt64Column(
    deephaven::dhcore::interop::NativePtr<deephaven::client::interop::ClientTableSpWrapper> self,
    int32_t column_index, int64_t *data,
    deephaven::dhcore::interop::InteropBool *optional_dest_null_flags, int64_t num_rows,
    deephaven::dhcore::interop::StringPoolHandle *string_pool_handle,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_TickingUpdate_dtor(deephaven::dhcore::ticking::TickingUpdate *self);

void deephaven_client_TickingUpdate_Current(deephaven::dhcore::ticking::TickingUpdate *self,
    deephaven::client::interop::ClientTableSpWrapper **result,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_AggregateCombo_Create(
    const deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *aggregate_ptrs,
    int32_t num_aggregates,
    deephaven::dhcore::interop::NativePtr<deephaven::client::AggregateCombo> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_AggregateCombo_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::AggregateCombo> self);

void deephaven_client_Aggregate_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> self);
void deephaven_client_Aggregate_AbsSum(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Group(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Avg(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Count(
    const char *column,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_First(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Last(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Max(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Med(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Min(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Pct(
    double percentile, deephaven::dhcore::interop::InteropBool avg_median,
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Std(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Sum(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_Var(
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_Aggregate_WAvg(const char *weight,
    const char **columns, int32_t num_columns,
    deephaven::dhcore::interop::NativePtr<deephaven::client::Aggregate> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);

void deephaven_client_utility_DurationSpecifier_ctor_nanos(
    int64_t nanos,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_utility_DurationSpecifier_ctor_durationstr(
    const char *duration_str,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_utility_DurationSpecifier_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::DurationSpecifier> result);

void deephaven_client_utility_TimePointSpecifier_ctor_nanos(
    int64_t nanos,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TimePointSpecifier> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_utility_TimePointSpecifier_ctor_timepointstr(
    const char *time_point_str,
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TimePointSpecifier> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_client_utility_TimePointSpecifier_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TimePointSpecifier> result);

void deephaven_dhclient_utility_TableMaker_ctor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_dtor(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self);
void deephaven_dhclient_utility_TableMaker_MakeTable(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    deephaven::dhcore::interop::NativePtr<deephaven::client::TableHandleManager> manager,
    deephaven::dhcore::interop::NativePtr<deephaven::client::TableHandle> *result,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__CharAsInt16(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const int16_t *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__Int8(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const int8_t *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__Int16(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const int16_t *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__Int32(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const int32_t *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__Int64(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const int64_t *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__Float(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const float *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__Double(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const double *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__BoolAsInteropBool(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const int8_t *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__String(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const char **data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
void deephaven_dhclient_utility_TableMaker_AddColumn__DateTimeAsInt64(
    deephaven::dhcore::interop::NativePtr<deephaven::client::utility::TableMaker> self,
    const char *name,
    const int64_t *data,
    int32_t length,
    const deephaven::dhcore::interop::InteropBool *optional_nulls,
    deephaven::dhcore::interop::ErrorStatusNew *status);
}  // extern "C"
