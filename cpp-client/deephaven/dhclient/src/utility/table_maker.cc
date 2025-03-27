/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/flight.h"
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/client/utility/arrow_util.h"
#include "deephaven/dhcore/types.h"
#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/third_party/fmt/format.h"

#include "arrow/array/builder_nested.h"

using deephaven::dhcore::DeephavenConstants;
using deephaven::dhcore::utility::MakeReservedVector;
using deephaven::client::TableHandle;
using deephaven::client::utility::OkOrThrow;
using deephaven::client::utility::ValueOrThrow;

#include <memory>

namespace deephaven::client::utility {
TableMaker::TableMaker() = default;
TableMaker::~TableMaker() = default;

void TableMaker::FinishAddColumn(std::string name, std::shared_ptr<arrow::Array> data,
    std::string deephaven_server_type_name) {
  if (!column_infos_.empty()) {
    auto num_rows = column_infos_.back().data_->length();
    if (data->length() != num_rows) {
      auto message = fmt::format("Column sizes not consistent: expected {}, have {}", num_rows,
          data->length());
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }
  }

  const auto &arrow_type = data->type();
  column_infos_.emplace_back(std::move(name), arrow_type, std::move(deephaven_server_type_name),
      std::move(data));
}

TableHandle TableMaker::MakeDeephavenTable(const TableHandleManager &manager) const {
  auto schema = MakeSchema();

  auto wrapper = manager.CreateFlightWrapper();
  auto ticket = manager.NewTicket();
  auto flight_descriptor = ArrowUtil::ConvertTicketToFlightDescriptor(ticket);

  arrow::flight::FlightCallOptions options;
  wrapper.AddHeaders(&options);

  auto res = wrapper.FlightClient()->DoPut(options, flight_descriptor, schema);
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res));
  auto data = GetColumnsNotEmpty();
  auto num_rows = data.back()->length();
  auto batch = arrow::RecordBatch::Make(schema, num_rows, std::move(data));

  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->WriteRecordBatch(*batch)));
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->DoneWriting()));

  std::shared_ptr<arrow::Buffer> buf;
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->reader->ReadMetadata(&buf)));
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->Close()));
  return manager.MakeTableHandleFromTicket(std::move(ticket));
}

std::shared_ptr<arrow::Table> TableMaker::MakeArrowTable() const {
  auto schema = MakeSchema();

  // Extract the data_vec column.
  auto data_vec = MakeReservedVector<std::shared_ptr<arrow::Array>>(column_infos_.size());
  for (const auto &info : column_infos_) {
    data_vec.push_back(info.data_);
  }
  auto data = GetColumnsNotEmpty();
  return arrow::Table::Make(std::move(schema), std::move(data));
}

std::vector<std::shared_ptr<arrow::Array>> TableMaker::GetColumnsNotEmpty() const {
  std::vector<std::shared_ptr<arrow::Array>> result;
  for (const auto &info : column_infos_) {
    result.emplace_back(info.data_);
  }
  if (result.empty()) {
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR("Can't make table with no columns"));
  }
  return result;
}

std::shared_ptr<arrow::Schema> TableMaker::MakeSchema() const {
  arrow::SchemaBuilder sb;
  for (const auto &info : column_infos_) {
    auto kv_metadata = std::make_shared<arrow::KeyValueMetadata>();
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(kv_metadata->Set("deephaven:type",
        info.deepaven_server_type_name_)));

    auto field = std::make_shared<arrow::Field>(info.name_, info.arrow_type_, true,
        std::move(kv_metadata));
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(sb.AddField(field)));
  }

  return ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(sb.Finish()));
}

namespace internal {
const char DeephavenServerConstants::kBool[] = "java.lang.Boolean";
const char DeephavenServerConstants::kChar16[] = "java.lang.Boolean";
std::string_view ColumnBuilder<int8_t>::GetDeephavenServerTypeName() { return "byte"; }
std::string_view ColumnBuilder<int16_t>::GetDeephavenServerTypeName() { return "short"; }
std::string_view ColumnBuilder<int32_t>::GetDeephavenServerTypeName() { return "int"; }
std::string_view ColumnBuilder<int64_t>::GetDeephavenServerTypeName() { return "long"; }
std::string_view ColumnBuilder<float>::GetDeephavenServerTypeName() { return "float"; }
std::string_view ColumnBuilder<double>::GetDeephavenServerTypeName() { return "double"; }
std::string_view ColumnBuilder<std::string>::GetDeephavenServerTypeName() { return "java.lang.String"; }
std::string_view ColumnBuilder<deephaven::dhcore::DateTime>::GetDeephavenServerTypeName() { return "java.time.ZonedDateTime"; }
std::string_view ColumnBuilder<deephaven::dhcore::LocalDate>::GetDeephavenServerTypeName() { return "java.time.LocalDate"; }
std::string_view ColumnBuilder<deephaven::dhcore::LocalTime>::GetDeephavenServerTypeName() { return "java.time.LocalTime"; }
}  // namespace internal
}  // namespace deephaven::client::utility
