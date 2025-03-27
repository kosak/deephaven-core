/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#pragma once

#include <optional>

#include <arrow/array.h>
#include <arrow/record_batch.h>
#include <arrow/scalar.h>
#include <arrow/type.h>
#include <arrow/table.h>
#include <arrow/flight/client.h>
#include <arrow/flight/types.h>
#include <arrow/array/array_primitive.h>
#include <arrow/array/builder_binary.h>
#include <arrow/array/builder_nested.h>
#include <arrow/array/builder_primitive.h>
#include <arrow/util/key_value_metadata.h>

#include "deephaven/client/client.h"
#include "deephaven/client/utility/arrow_util.h"
#include "deephaven/client/utility/internal_types.h"
#include "deephaven/client/utility/misc_types.h"
#include "deephaven/dhcore/types.h"
#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/third_party/fmt/format.h"

namespace deephaven::client::utility {
namespace internal {
template<typename T>
struct ColumnBuilder {
  // The below assert fires when this class is instantiated; i.e. when none of the specializations
  // match. It needs to be written this way (with "is_same<T,T>") because for technical reasons it
  // needs to be dependent on T, even if degenerately so.
  static_assert(!std::is_same_v<T, T>, "ColumnBuilder doesn't know how to work with this type");
};

template<typename T, typename TArrowBuilder, const char *kDeephavenTypeName>
struct SimpleBuilderBase {
  void Append(const T &value) {
    builder_->Append(value);
  }
  void AppendNull() {
    builder_->AppendNull();
  }
  std::shared_ptr<arrow::Array> Finish() {
    return ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Finish()));
  }
  std::string_view GetDeephavenServerTypeName() {
    return kDeephavenTypeName;
  }

  std::shared_ptr<TArrowBuilder> builder_;
};

struct DeephavenServerConstants {
  static const char kBool[];
  static const char kChar16[];
  static const char kInt8[];
};

template<>
struct ColumnBuilder<bool> : public SimpleBuilderBase<bool, arrow::BooleanBuilder,
    DeephavenServerConstants::kBool> {
};

template<>
struct ColumnBuilder<char16_t> : public SimpleBuilderBase<char16_t, arrow::UInt16Builder,
    DeephavenServerConstants::kChar16> {
};

template<>
struct ColumnBuilder<int8_t> : public SimpleBuilderBase<int8_t, arrow::Int8Builder,
    DeephavenServerConstants::kInt8> {
};

template<>
struct ColumnBuilder<int16_t> {
  void Append(int16_t value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::Int16Builder> builder_;
};

template<>
struct ColumnBuilder<int32_t> {
  void Append(int32_t value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::Int32Builder> builder_;
};

template<>
struct ColumnBuilder<int64_t> {
  void Append(int64_t value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::Int64Builder> builder_;
};

template<>
struct ColumnBuilder<float> {
  void Append(float value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::FloatBuilder> builder_;
};

template<>
struct ColumnBuilder<double> {
  void Append(double value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::DoubleBuilder> builder_;
};

template<>
struct ColumnBuilder<std::string> {
  void Append(const std::string &value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::StringBuilder> builder_;
};

template<>
struct ColumnBuilder<deephaven::dhcore::DateTime> {
  void Append(const deephaven::dhcore::DateTime &value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::TimestampBuilder> builder_;
};

template<>
struct ColumnBuilder<deephaven::dhcore::LocalDate> {
  void Append(const deephaven::dhcore::LocalDate &value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::Date64Builder> builder_;
};

template<>
struct ColumnBuilder<deephaven::dhcore::LocalTime> {
  void Append(const deephaven::dhcore::LocalTime &value);
  void AppendNull();
  std::shared_ptr<arrow::Array> Finish();
  std::string_view GetDeephavenServerTypeName();

  std::shared_ptr<arrow::Time64Builder> builder_;
};

template<typename T>
class ColumnBuilder<std::optional<T>> {
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

  std::string_view GetDeephavenServerTypeName();

  ColumnBuilder<T> inner_builder_;
};

template<typename T>
class ColumnBuilder<std::vector<T>> {
public:
  ColumnBuilder() :
      builder_(std::make_shared<arrow::ListBuilder>(arrow::default_memory_pool(),
          inner_builder_.builder_)) {
  }

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

  std::string_view GetDeephavenServerTypeName();

  ColumnBuilder<T> inner_builder_;
  std::shared_ptr<arrow::ListBuilder> builder_;
};
}  // namespace internal

/**
 * A convenience class for populating small tables. It is a wrapper around Arrow Flight's
 * DoPut functionality. Typical usage
 * @code
 * TableMaker tm;
 * std::vector<T1> data1 = { ... };
 * std::vector<T2> data2 = { ... };
 * tm.AddColumn("col1", data1);
 * tm.AddColumn("col2", data2);
 * auto arrow_table = tm.MakeArrowTable();
 * auto table_handle = tm.MakeDeephavenTable(const TableHandleManager &manager);
 * @endcode
 */
class TableMaker {
  using TableHandleManager = deephaven::client::TableHandleManager;
  using TableHandle = deephaven::client::TableHandle;
public:
  /**
   * Constructor
   */
  TableMaker();
  /**
   * Destructor
   */
  ~TableMaker();

  /**
   * Creates a column whose server type most closely matches type T, having the given name and
   * values. Each call to this method adds a column. When there are multiple calls to this method,
   * the sizes of the `values` arrays must be consistent across those calls. That is, when the
   * table has multiple columns, they all have to have the same number of rows.
   */
  template<typename T>
  void AddColumn(std::string name, const std::vector<T> &values) {
    internal::ColumnBuilder<T> cb;
    for (const auto &element : values) {
      cb.Append(element);
    }
    auto [array, dh_type] = cb.Finish();
    FinishAddColumn(std::move(name), std::move(array), std::move(dh_type));
  }

  template<typename T, typename GetValue, typename IsNull>
  void AddColumn(std::string name, const GetValue &get_value, const IsNull &is_null,
      size_t size);

  /**
   * Make a table on the Deephaven server based on all the AddColumn calls you have made so far.
   * @param manager The TableHandleManager
   * @return The TableHandle referencing the newly-created table.
   */
  [[nodiscard]]
  TableHandle MakeDeephavenTable(const TableHandleManager &manager) const;

  [[nodiscard]]
  std::shared_ptr<arrow::Table> MakeArrowTable() const;

private:
  void FinishAddColumn(std::string name, std::shared_ptr<arrow::Array> data,
      std::string deephaven_server_type_name);
  std::shared_ptr<arrow::Schema> MakeSchema() const;
  std::vector<std::shared_ptr<arrow::Array>> GetColumnsNotEmpty() const;

  struct ColumnInfo {
    ColumnInfo(std::string name, std::shared_ptr<arrow::DataType> arrow_type,
        std::string deepaven_type, std::shared_ptr<arrow::Array> data);
    ~ColumnInfo() = default;

    std::string name_;
    std::shared_ptr<arrow::DataType> arrow_type_;
    std::string deepaven_server_type_name_;
    std::shared_ptr<arrow::Array> data_;
  };

  std::vector<ColumnInfo> column_infos_;
};

namespace internal {


template<>
struct TypeConverterTraits<deephaven::dhcore::DateTime> {
  static std::shared_ptr<arrow::DataType> GetDataType() {
    return arrow::timestamp(arrow::TimeUnit::NANO, "UTC");
  }
  static arrow::TimestampBuilder GetBuilder() {
    return arrow::TimestampBuilder(GetDataType(), arrow::default_memory_pool());
  }
  static int64_t Reinterpret(const deephaven::dhcore::DateTime &dt) {
    return dt.Nanos();
  }
  static std::string_view GetDeephavenTypeName() {
    return "java.time.ZonedDateTime";
  }
};

template<>
struct TypeConverterTraits<deephaven::dhcore::LocalDate> {
  static std::shared_ptr<arrow::DataType> GetDataType() {
    return arrow::date64();
  }
  static arrow::Date64Builder GetBuilder() {
    return arrow::Date64Builder();
  }
  static int64_t Reinterpret(const deephaven::dhcore::LocalDate &o) {
    return o.Millis();
  }
  static std::string_view GetDeephavenTypeName() {
    return "java.time.LocalDate";
  }
};

template<>
struct TypeConverterTraits<deephaven::dhcore::LocalTime> {
  static std::shared_ptr<arrow::DataType> GetDataType() {
    return arrow::time64(arrow::TimeUnit::NANO);
  }
  static arrow::Time64Builder GetBuilder() {
    return arrow::Time64Builder(GetDataType(), arrow::default_memory_pool());
  }
  static int64_t Reinterpret(const deephaven::dhcore::LocalTime &o) {
    return o.Nanos();
  }
  static std::string_view GetDeephavenTypeName() {
    return "java.time.LocalTime";
  }
};

template<typename T>
struct TypeConverterTraits<std::optional<T>> {
  using inner_t = TypeConverterTraits<T>;
  static auto GetDataType() {
    return inner_t::GetDataType();
  }
  static auto GetBuilder() {
    return inner_t::GetBuilder();
  }
  static auto Reinterpret(const T &o) {
    return inner_t::Reinterpret(o);
  }
  static std::string_view GetDeephavenTypeName() {
    return TypeConverterTraits<T>::GetDeephavenTypeName();
  }
};

template<arrow::TimeUnit::type UNIT>
struct TypeConverterTraits<deephaven::client::utility::internal::InternalDateTime<UNIT>> {
  static std::shared_ptr<arrow::DataType> GetDataType() {
    return arrow::timestamp(UNIT, "UTC");
  }
  static arrow::TimestampBuilder GetBuilder() {
    return arrow::TimestampBuilder(GetDataType(), arrow::default_memory_pool());
  }
  static int64_t Reinterpret(const deephaven::client::utility::internal::InternalDateTime<UNIT> &o) {
    return o.value_;
  }
  static std::string_view GetDeephavenTypeName() {
    return "java.time.ZonedDateTime";
  }
};

template<arrow::TimeUnit::type UNIT>
struct TypeConverterTraits<deephaven::client::utility::internal::InternalLocalTime<UNIT>> {
  static std::shared_ptr<arrow::DataType> GetDataType() {
    return arrow::time64(UNIT);
  }
  static arrow::Time64Builder GetBuilder() {
    return arrow::Time64Builder(GetDataType(), arrow::default_memory_pool());
  }
  static int64_t Reinterpret(const deephaven::client::utility::internal::InternalLocalTime<UNIT> &o) {
    return o.value_;
  }
  static std::string_view GetDeephavenTypeName() {
    return "java.time.LocalTime";
  }
};

template<typename T>
TypeConverter TypeConverter::CreateNew(const std::vector<T> &values) {
  using deephaven::client::utility::OkOrThrow;

  typedef TypeConverterTraits<T> traits_t;

  auto data_type = traits_t::GetDataType();
  auto builder = traits_t::GetBuilder();

  for (const auto &value : values) {
    bool valid;
    const auto *contained_value = TryGetContainedValue(&value, &valid);
    if (valid) {
      OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder.Append(traits_t::Reinterpret(*contained_value))));
    } else {
      OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder.AppendNull()));
    }
  }
  auto builder_res = builder.Finish();
  if (!builder_res.ok()) {
    auto message = fmt::format("Error building array of type {}: {}",
        traits_t::GetDeephavenTypeName(), builder_res.status().ToString());
  }
  auto array = builder_res.ValueUnsafe();
  return TypeConverter(std::move(data_type), std::string(traits_t::GetDeephavenTypeName()),
      std::move(array));
}

template<typename T, typename GetValue, typename IsNull>
TypeConverter TypeConverter::CreateNew(const GetValue &get_value, const IsNull &is_null,
    size_t size) {
  using deephaven::client::utility::OkOrThrow;

  typedef TypeConverterTraits<T> traits_t;

  auto data_type = traits_t::GetDataType();
  auto builder = traits_t::GetBuilder();

  for (size_t i = 0; i != size; ++i) {
    if (!is_null(i)) {
       OkOrThrow(DEEPHAVEN_LOCATION_EXPR(
           builder.Append(traits_t::Reinterpret(get_value(i)))));
    } else {
      OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder.AppendNull()));
    }
  }
  auto builder_res = builder.Finish();
  if (!builder_res.ok()) {
    auto message = fmt::format("Error building array of type {}: {}",
        traits_t::GetDeephavenTypeName(), builder_res.status().ToString());
  }
  auto array = builder_res.ValueUnsafe();
  return TypeConverter(std::move(data_type), std::string(traits_t::GetDeephavenTypeName()),
      std::move(array));
}
}  // namespace internal
}  // namespace deephaven::client::utility
