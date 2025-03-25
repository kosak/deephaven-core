/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/flight.h"
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/client/utility/arrow_util.h"
#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/third_party/fmt/format.h"

#include "arrow/array/builder_nested.h"

using deephaven::client::TableHandle;
using deephaven::client::utility::OkOrThrow;
using deephaven::client::utility::ValueOrThrow;

#include <memory>

namespace kosak_alt {
template<typename T>
class ZamboniBuilder;

template<>
class ZamboniBuilder<int32_t> {
public:
  void Append(int32_t value) {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Append(value)));
  }

  void AppendNull() {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->AppendNull()));
  }

//  void AppendValues(const std::vector<int32_t> &values) {
//    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->AppendValues(values)));
//  }

  std::shared_ptr<arrow::Array> Finish() {
    return ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Finish()));
  }

  std::shared_ptr<arrow::Int32Builder> builder_;
};

template<typename T>
class ZamboniBuilder<std::optional<T>> {
public:
  void Append(const std::optional<T> &value) {
    if (!value.has_value()) {
      inner_builder_.AppendNull();
    } else {
      inner_builder_.Append(*value);
    }
  }

  void AppendNull() {
    inner_builder_.AppendNull();
  }

  std::shared_ptr<arrow::Array> Finish() {
    return inner_builder_.Finish();
  }

//  void AppendValues(const std::vector<std::optional<T>> &values) {
//    for (const auto &opt : values) {
//      if (!opt.has_value()) {
//        inner_builder_.AppendNull();
//      } else {
//        inner_builder_.Append(*opt);
//      }
//    }
//  }

  ZamboniBuilder<T> inner_builder_;
};


template<typename T>
class ZamboniBuilder<std::vector<T>> {
public:
  ZamboniBuilder() :
    builder_(std::make_shared<arrow::ListBuilder>(arrow::default_memory_pool(), inner_builder_.builder_)) {
  }

//  void AppendValues(const std::vector<std::vector<T>> &values) {
//    for (const auto &entry : values) {
//      inner_builder_.AppendValues(entry);
//      OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Append()));
//    }
//  }

  void Append(const std::vector<T> &entry) {
    for (const auto &element : entry) {
      inner_builder_.Append(element);
      OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Append()));
    }
  }

  void AppendNull() {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->AppendNull()));
  }

  std::shared_ptr<arrow::Array> Finish() {
    return ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Finish()));
  }

  ZamboniBuilder<T> inner_builder_;
  std::shared_ptr<arrow::ListBuilder> builder_;
};

void kosak_test() {
  ZamboniBuilder<int32_t> b1;
  std::vector<int32_t> v1;
  for (const auto &element : v1) {
    b1.Append(element);
  }
  b1.Finish();

  ZamboniBuilder<std::optional<int32_t>> b2;
  std::vector<std::optional<int32_t>> v2;
  for (const auto &element : v2) {
    b2.Append(element);
  }
  b2.Finish();

  ZamboniBuilder<std::vector<int32_t>> b3;
  std::vector<std::vector<int32_t>> v3;
  for (const auto &element : v3) {
    b3.Append(element);
  }
  b3.Finish();

  ZamboniBuilder<std::optional<std::vector<int32_t>>> b4;
  std::vector<std::optional<std::vector<int32_t>>> v4;
  for (const auto &element : v4) {
    b4.Append(element);
  }
  b4.Finish();

  ZamboniBuilder<std::vector<std::vector<int32_t>>> b5;
  std::vector<std::vector<std::vector<int32_t>>> v5;
  for (const auto &element : v5) {
    b5.Append(element);
  }
  b5.Finish();

}
}

namespace deephaven::client::utility {
TableMaker::TableMaker() = default;
TableMaker::~TableMaker() = default;

void TableMaker::FinishAddColumn(std::string name, internal::TypeConverter info) {
  auto kv_metadata = std::make_shared<arrow::KeyValueMetadata>();
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(kv_metadata->Set("deephaven:type", info.DeephavenType())));

  auto field = std::make_shared<arrow::Field>(std::move(name), std::move(info.DataType()), true,
      std::move(kv_metadata));
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(schemaBuilder_.AddField(field)));

  if (columns_.empty()) {
    numRows_ = info.Column()->length();
  } else if (numRows_ != info.Column()->length()) {
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR(
        fmt::format("Column sizes not consistent: expected {}, have {}", numRows_,
            info.Column()->length())));
  }

  columns_.push_back(std::move(info.Column()));
}

TableHandle TableMaker::MakeTable(const TableHandleManager &manager) {
  auto schema = ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(schemaBuilder_.Finish()));

  auto wrapper = manager.CreateFlightWrapper();
  auto ticket = manager.NewTicket();
  auto flight_descriptor = ArrowUtil::ConvertTicketToFlightDescriptor(ticket);

  arrow::flight::FlightCallOptions options;
  wrapper.AddHeaders(&options);

  auto res = wrapper.FlightClient()->DoPut(options, flight_descriptor, schema);
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res));
  auto batch = arrow::RecordBatch::Make(schema, numRows_, std::move(columns_));

  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->WriteRecordBatch(*batch)));
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->DoneWriting()));

  std::shared_ptr<arrow::Buffer> buf;
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->reader->ReadMetadata(&buf)));
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->Close()));
  return manager.MakeTableHandleFromTicket(std::move(ticket));
}

namespace internal {
TypeConverter::TypeConverter(std::shared_ptr<arrow::DataType> data_type,
    std::string deephaven_type, std::shared_ptr<arrow::Array> column) :
    dataType_(std::move(data_type)), deephavenType_(std::move(deephaven_type)),
    column_(std::move(column)) {}
    TypeConverter::~TypeConverter() = default;
}  // namespace internal
}  // namespace deephaven::client::utility
