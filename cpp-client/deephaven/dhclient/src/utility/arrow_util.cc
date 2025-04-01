/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/utility/arrow_util.h"

#include <cstddef>
#include <cstdint>
#include <memory>
#include <stdexcept>
#include <string>
#include <optional>
#include <utility>

#include <arrow/array/builder_binary.h>
#include <arrow/array/builder_primitive.h>
#include <arrow/status.h>
#include <arrow/flight/types.h>
#include <arrow/table.h>
#include <arrow/type.h>
#include <arrow/visitor.h>
#include "deephaven/dhcore/chunk/chunk.h"
#include "deephaven/dhcore/clienttable/schema.h"
#include "deephaven/dhcore/column/column_source.h"
#include "deephaven/dhcore/container/row_sequence.h"
#include "deephaven/dhcore/types.h"
#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/third_party/fmt/core.h"

using deephaven::dhcore::chunk::BooleanChunk;
using deephaven::dhcore::chunk::CharChunk;
using deephaven::dhcore::chunk::DateTimeChunk;
using deephaven::dhcore::chunk::DoubleChunk;
using deephaven::dhcore::chunk::FloatChunk;
using deephaven::dhcore::chunk::Int8Chunk;
using deephaven::dhcore::chunk::Int16Chunk;
using deephaven::dhcore::chunk::Int32Chunk;
using deephaven::dhcore::chunk::Int64Chunk;
using deephaven::dhcore::chunk::LocalDateChunk;
using deephaven::dhcore::chunk::LocalTimeChunk;
using deephaven::dhcore::chunk::StringChunk;
using deephaven::dhcore::clienttable::Schema;
using deephaven::dhcore::column::ColumnSource;
using deephaven::dhcore::column::ColumnSourceVisitor;
using deephaven::dhcore::container::RowSequence;
using deephaven::dhcore::DateTime;
using deephaven::dhcore::LocalDate;
using deephaven::dhcore::LocalTime;
using deephaven::dhcore::ElementTypeId;
using deephaven::dhcore::utility::MakeReservedVector;

namespace deephaven::client::utility {
void OkOrThrow(const deephaven::dhcore::utility::DebugInfo &debug_info,
    const arrow::Status &status) {
  if (status.ok()) {
    return;
  }

  auto msg = fmt::format("Status: {}. Caller: {}", status.ToString(), debug_info);
  throw std::runtime_error(msg);
}

arrow::flight::FlightDescriptor ArrowUtil::ConvertTicketToFlightDescriptor(const std::string &ticket) {
  if (ticket.length() != 5 || ticket[0] != 'e') {
    const char *message = "Ticket is not in correct format for export";
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
  }
  uint32_t value;
  memcpy(&value, ticket.data() + 1, sizeof(uint32_t));
  return arrow::flight::FlightDescriptor::Path({"export", std::to_string(value)});
};

namespace {
struct ArrowToElementTypeId final : public arrow::TypeVisitor {
  arrow::Status Visit(const arrow::Int8Type &/*type*/) final {
    type_id_ = ElementTypeId::kInt8;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int16Type &/*type*/) final {
    type_id_ = ElementTypeId::kInt16;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int32Type &/*type*/) final {
    type_id_ = ElementTypeId::kInt32;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int64Type &/*type*/) final {
    type_id_ = ElementTypeId::kInt64;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::FloatType &/*type*/) final {
    type_id_ = ElementTypeId::kFloat;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::DoubleType &/*type*/) final {
    type_id_ = ElementTypeId::kDouble;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::BooleanType &/*type*/) final {
    type_id_ = ElementTypeId::kBool;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::UInt16Type &/*type*/) final {
    type_id_ = ElementTypeId::kChar;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::StringType &/*type*/) final {
    type_id_ = ElementTypeId::kString;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::TimestampType &/*type*/) final {
    type_id_ = ElementTypeId::kTimestamp;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::ListType &/*type*/) final {
    type_id_ = ElementTypeId::kList;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Time64Type &/*type*/) final {
    type_id_ = ElementTypeId::kLocalTime;
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Date64Type &type) final {
    if (type.unit() != arrow::DateUnit::MILLI) {
      auto message = fmt::format("Expected Date64Type with milli units, got {}",
          type.ToString());
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }
    type_id_ = ElementTypeId::kLocalDate;
    return arrow::Status::OK();
  }

  ElementTypeId::Enum type_id_ = ElementTypeId::kInt8;  // arbitrary initializer
};
}  // namespace

std::optional<ElementTypeId::Enum> ArrowUtil::GetElementTypeId(const arrow::DataType &data_type,
    bool must_succeed) {
  ArrowToElementTypeId visitor;
  auto result = data_type.Accept(&visitor);
  if (result.ok()) {
    return visitor.type_id_;
  }
  if (!must_succeed) {
    return {};
  }
  auto message = fmt::format("Can't find Deephaven mapping for arrow data type {}",
      data_type.ToString());
  throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
}

std::shared_ptr<arrow::DataType> ArrowUtil::GetArrowType(ElementTypeId::Enum element_type_id) {
  switch (element_type_id) {
    case ElementTypeId::Enum::kChar: return std::make_shared<arrow::UInt16Type>();
    case ElementTypeId::Enum::kInt8: return std::make_shared<arrow::Int8Type>();
    case ElementTypeId::Enum::kInt16: return std::make_shared<arrow::Int16Type>();
    case ElementTypeId::Enum::kInt32: return std::make_shared<arrow::Int32Type>();
    case ElementTypeId::Enum::kInt64: return std::make_shared<arrow::Int64Type>();
    case ElementTypeId::Enum::kFloat: return std::make_shared<arrow::FloatType>();
    case ElementTypeId::Enum::kDouble: return std::make_shared<arrow::DoubleType>();
    case ElementTypeId::Enum::kBool: return std::make_shared<arrow::BooleanType>();
    case ElementTypeId::Enum::kString: return std::make_shared<arrow::StringType>();
    case ElementTypeId::Enum::kTimestamp: return std::make_shared<arrow::TimestampType>(
        arrow::TimeUnit::NANO, "UTC");
    case ElementTypeId::Enum::kList: {
      // TODO(kosak)
      auto underlying = std::make_shared<arrow::Int32Type>();
      return std::make_shared<arrow::ListType>(underlying);
    }
    case ElementTypeId::Enum::kLocalDate: return std::make_shared<arrow::Date64Type>();
    case ElementTypeId::Enum::kLocalTime: return std::make_shared<arrow::Time64Type>(arrow::TimeUnit::NANO);
    default: {
      auto message = fmt::format("Unexpected element_type_id {}", static_cast<int>(element_type_id));
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }
  }
}

std::shared_ptr<Schema> ArrowUtil::MakeDeephavenSchema(const arrow::Schema &schema) {
  const auto &fields = schema.fields();
  auto names = MakeReservedVector<std::string>(fields.size());
  auto types = MakeReservedVector<ElementTypeId::Enum>(fields.size());
  for (const auto &f: fields) {
    auto type_id = ArrowUtil::GetElementTypeId(*f->type(), true);
    names.push_back(f->name());
    types.push_back(*type_id);
  }
  return Schema::Create(std::move(names), std::move(types));
}

namespace {
struct Visitor final : ColumnSourceVisitor {
  explicit Visitor(size_t num_rows) : num_rows_(num_rows),
    row_sequence_(RowSequence::CreateSequential(0, num_rows)),
    null_flags_(BooleanChunk::Create(num_rows)) {
  }

  void Visit(const dhcore::column::CharColumnSource &source) final {
    arrow::UInt16Builder builder;
    auto converter = [](char16_t ch) {
      return static_cast<uint16_t>(ch);
    };
    CopyValuesWithConversion<CharChunk>(source, &builder, converter);
  }

  void Visit(const dhcore::column::Int8ColumnSource &source) final {
    CopyValues<Int8Chunk, arrow::Int8Builder>(source);
  }

  void Visit(const dhcore::column::Int16ColumnSource &source) final {
    CopyValues<Int16Chunk, arrow::Int16Builder>(source);
  }

  void Visit(const dhcore::column::Int32ColumnSource &source) final {
    CopyValues<Int32Chunk, arrow::Int32Builder>(source);
  }

  void Visit(const dhcore::column::Int64ColumnSource &source) final {
    CopyValues<Int64Chunk, arrow::Int64Builder>(source);
  }

  void Visit(const dhcore::column::FloatColumnSource &source) final {
    CopyValues<FloatChunk, arrow::FloatBuilder>(source);
  }

  void Visit(const dhcore::column::DoubleColumnSource &source) final {
    CopyValues<DoubleChunk, arrow::DoubleBuilder>(source);
  }

  void Visit(const dhcore::column::BooleanColumnSource &source) final {
    arrow::BooleanBuilder builder;
    auto converter = [](bool b) {
      return static_cast<uint8_t>(b);
    };
    CopyValuesWithConversion<BooleanChunk>(source, &builder, converter);
  }

  void Visit(const dhcore::column::StringColumnSource &source) final {
    arrow::StringBuilder builder;
    auto converter = [](const std::string &s) -> const std::string & {
      return s;
    };
    CopyValuesWithConversion<StringChunk>(source, &builder, converter);
  }

  void Visit(const dhcore::column::DateTimeColumnSource &source) final {
    arrow::TimestampBuilder builder(arrow::timestamp(arrow::TimeUnit::NANO, "UTC"),
        arrow::default_memory_pool());
    auto converter = [](const DateTime &dt) {
      return dt.Nanos();
    };
    CopyValuesWithConversion<DateTimeChunk>(source, &builder, converter);
  }

  void Visit(const dhcore::column::LocalDateColumnSource &source) final {
    arrow::Date64Builder builder;
    auto converter = [](const LocalDate &ld) {
      return ld.Millis();
    };
    CopyValuesWithConversion<LocalDateChunk>(source, &builder, converter);
  }

  void Visit(const dhcore::column::LocalTimeColumnSource &source) final {
    arrow::Time64Builder builder(arrow::time64(arrow::TimeUnit::NANO), arrow::default_memory_pool());
    auto converter = [](const LocalTime &lt) {
      return lt.Nanos();
    };
    CopyValuesWithConversion<LocalTimeChunk>(source, &builder, converter);
  }

  void Visit(const dhcore::column::ContainerBaseColumnSource &source) final {
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR("TODO(kosak)"));
  }

  template<typename TChunk, typename TBuilder, typename TColumnSource>
  void CopyValues(const TColumnSource &source) {
    auto values = TChunk::Create(num_rows_);
    source.FillChunk(*row_sequence_, &values, &null_flags_);
    auto validity = MakeValidity();
    TBuilder builder;
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder.AppendValues(values.data(), num_rows_, validity.get())));
    result_ = ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(builder.Finish()));
  }

  template<typename TChunk, typename TColumnSource, typename TBuilder, typename TConverter>
  void CopyValuesWithConversion(const TColumnSource &source, TBuilder *builder,
      const TConverter &converter) {
    auto values = TChunk::Create(num_rows_);
    source.FillChunk(*row_sequence_, &values, &null_flags_);
    auto validity = MakeValidity();
    for (const auto &value : values) {
      OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder->Append(converter(value))));
    }
    result_ = ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(builder->Finish()));
  }

  std::unique_ptr<uint8_t[]> MakeValidity() {
    auto result = std::make_unique<uint8_t[]>(num_rows_);

    for (size_t i = 0; i != num_rows_; ++i) {
      // Invert
      result[i] = null_flags_.data()[i] ? 0 : 1;
    }

    return result;
  }

  size_t num_rows_;
  std::shared_ptr<RowSequence> row_sequence_;
  BooleanChunk null_flags_;
  std::shared_ptr<arrow::Array> result_;
};
}  // namespace

std::shared_ptr<arrow::Array> ArrowUtil::MakeArrowArray(const ColumnSource &column_source,
    size_t num_rows) {
  Visitor visitor(num_rows);
  column_source.AcceptVisitor(&visitor);
  return std::move(visitor.result_);
}

std::shared_ptr<arrow::Table> ArrowUtil::MakeArrowTable(const ClientTable &client_table) {
  auto ncols = client_table.NumColumns();
  auto nrows = client_table.NumRows();
  auto arrays = MakeReservedVector<std::shared_ptr<arrow::Array>>(ncols);

  for (size_t i = 0; i != ncols; ++i) {
    auto column_source = client_table.GetColumn(i);
    auto arrow_array = MakeArrowArray(*column_source, nrows);
    arrays.emplace_back(std::move(arrow_array));
  }

  auto schema = MakeArrowSchema(*client_table.Schema());

  return arrow::Table::Make(std::move(schema), arrays);
}

std::shared_ptr<arrow::Schema> ArrowUtil::MakeArrowSchema(
    const deephaven::dhcore::clienttable::Schema &dh_schema) {
  arrow::SchemaBuilder builder;
  for (int32_t i = 0; i != dh_schema.NumCols(); ++i) {
    const auto &name = dh_schema.Names()[i];
    auto element_type = dh_schema.Types()[i];
    auto arrow_type = GetArrowType(element_type);
    auto field = std::make_shared<arrow::Field>(name, std::move(arrow_type));
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder.AddField(field)));
  }
  return ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(builder.Finish()));
}
}  // namespace deephaven::client::utility
