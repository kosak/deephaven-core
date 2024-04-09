/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include <cstdint>
#include "deephaven/client/client.h"
#include "deephaven/client/client_options.h"
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
}  // namespace deephaven::client::interop {



extern "C" {
void invokelab_s1(const char *s);
void invokelab_s2(const char16_t *s);
void invokelab_s3(const char **ss);
void invokelab_s4(const char16_t **ss);

void invokelab_r0(const char16_t *s);
const deephaven::dhcore::interop::PlatformUtf16 *invokelab_r1();
const deephaven::dhcore::interop::PlatformUtf16 *invokelab_r2(const char16_t *s);
void invokelab_r3(const char16_t **data_in, int32_t count);
void invokelab_r4(const deephaven::dhcore::interop::PlatformUtf16 **data_out, int32_t count);
void invokelab_r5(const char16_t **data_in, const deephaven::dhcore::interop::PlatformUtf16 **data_out,
    int32_t count);

// TODO(kosak): all the const char * should maybe come over as UTF-16 types from Windows.

void deephaven_client_TableHandleManager_dtor(deephaven::client::TableHandleManager *self);
void deephaven_client_TableHandleManager_EmptyTable(const deephaven::client::TableHandleManager *self,
    int64_t size,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandle> *roe);
void deephaven_client_TableHandleManager_FetchTable(const deephaven::client::TableHandleManager *self,
    const char16_t *table_name,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandle> *roe);
void deephaven_client_TableHandleManager_TimeTable(const deephaven::client::TableHandleManager *self,
    const deephaven::client::utility::DurationSpecifier *period,
    const deephaven::client::utility::TimePointSpecifier *start_time,
    bool blink_table,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandle> *roe);
void deephaven_client_TableHandleManager_InputTable(const deephaven::client::TableHandleManager *self,
    const deephaven::client::TableHandle *initial_table, const char16_t **key_columns,
    int64_t num_key_columns,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandle> *roe);
void deephaven_client_TableHandleManager_RunScript(const deephaven::client::TableHandleManager *self,
    const char16_t *code,
    deephaven::dhcore::interop::ResultOrError<void> *roe);

void deephaven_client_Client_Connect(const char16_t *target,
    const deephaven::client::ClientOptions *options,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::Client> *roe);
void deephaven_client_Client_dtor(deephaven::client::Client *self);
void deephaven_client_Client_Close(deephaven::client::Client *self,
    deephaven::dhcore::interop::ResultOrError<void> *roe);
void deephaven_client_Client_GetManager(deephaven::client::Client *self,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandleManager> *roe);

void deephaven_client_TableHandle_dtor(deephaven::client::TableHandle *self);
void deephaven_client_TableHandle_GetManager(deephaven::client::TableHandle *self,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandleManager> *roe);
void deephaven_client_TableHandle_Select(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandle> *roe);
void deephaven_client_TableHandle_View(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandle> *roe);
void deephaven_client_TableHandle_DropColumns(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandle> *roe);
void deephaven_client_TableHandle_Update(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::dhcore::interop::ResultOrError<deephaven::client::TableHandle> *roe);
// ...
void deephaven_client_TableHandle_BindToVariable(deephaven::client::TableHandle *self,
    const char16_t *variable,
    deephaven::dhcore::interop::ResultOrError<void> *roe);
void deephaven_client_TableHandle_ToString(
    deephaven::client::TableHandle *self,
    int32_t want_headers,
    deephaven::dhcore::interop::PlatformUtf16v2 *result,
    deephaven::dhcore::interop::ErrorStatus *status);
void deephaven_client_TableHandle_ToArrowTable(deephaven::client::TableHandle *self,
    deephaven::client::interop::ArrowTable **arrow_table, int32_t *num_columns, int64_t *num_rows,
    deephaven::dhcore::interop::ErrorStatus *status);

void deephaven_client_ArrowTable_dtor(deephaven::client::interop::ArrowTable *self);

void deephaven_client_ArrowTable_GetSchema(deephaven::client::interop::ArrowTable *self,
    int32_t num_columns, deephaven::dhcore::interop::PlatformUtf16v2 *columns,
    int32_t *column_types, deephaven::dhcore::interop::ErrorStatus *status);
}
