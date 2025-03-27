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

template<typename TArrowBuilder, const char *kDeephavenTypeName>
struct BuilderBase {
  explicit BuilderBase(std::shared_ptr<TArrowBuilder> builder) : builder_(std::move(builder)) {}

  void AppendNull() {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->AppendNull()));
  }
  std::shared_ptr<arrow::Array> Finish() {
    return ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Finish()));
  }
  const char *GetDeephavenServerTypeName() {
    return kDeephavenTypeName;
  }

  std::shared_ptr<TArrowBuilder> builder_;
};

template<typename T, typename TArrowBuilder, const char *kDeephavenTypeName>
struct TypicalBuilderBase : public BuilderBase<TArrowBuilder, kDeephavenTypeName> {
  /**
   * Convenience using.
   */
  using base = BuilderBase<TArrowBuilder, kDeephavenTypeName>;

  TypicalBuilderBase() : BuilderBase<TArrowBuilder, kDeephavenTypeName>(
      std::make_shared<TArrowBuilder>()) {
  }

  void Append(const T &value) {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(base::builder_->Append(value)));
  }
};

struct DeephavenServerConstants {
  static const char kBool[];
  static const char kChar16[];
  static const char kInt8[];
  static const char kInt16[];
  static const char kInt32[];
  static const char kInt64[];
  static const char kFloat[];
  static const char kDouble[];
  static const char kString[];
  static const char kDateTime[];
  static const char kLocalDate[];
  static const char kLocalTime[];
};

template<>
struct ColumnBuilder<bool> : public TypicalBuilderBase<bool,
    arrow::BooleanBuilder,
    DeephavenServerConstants::kBool> {
};

template<>
struct ColumnBuilder<char16_t> : public TypicalBuilderBase<char16_t, arrow::UInt16Builder,
    DeephavenServerConstants::kChar16> {
};

template<>
struct ColumnBuilder<int8_t> : public TypicalBuilderBase<int8_t, arrow::Int8Builder,
    DeephavenServerConstants::kInt8> {
};

template<>
struct ColumnBuilder<int16_t> : public TypicalBuilderBase<int16_t, arrow::Int16Builder,
    DeephavenServerConstants::kInt16> {
};

template<>
struct ColumnBuilder<int32_t> : public TypicalBuilderBase<int32_t, arrow::Int32Builder,
    DeephavenServerConstants::kInt32> {
};

template<>
struct ColumnBuilder<int64_t> : public TypicalBuilderBase<int64_t, arrow::Int64Builder,
    DeephavenServerConstants::kInt64> {
};

template<>
struct ColumnBuilder<float> : public TypicalBuilderBase<float, arrow::FloatBuilder,
    DeephavenServerConstants::kFloat> {
};

template<>
struct ColumnBuilder<double> : public TypicalBuilderBase<double, arrow::DoubleBuilder,
    DeephavenServerConstants::kDouble> {
};

template<>
struct ColumnBuilder<std::string> : public TypicalBuilderBase<std::string, arrow::StringBuilder,
    DeephavenServerConstants::kString> {
};

template<>
struct ColumnBuilder<deephaven::dhcore::DateTime> : public BuilderBase<arrow::TimestampBuilder,
    DeephavenServerConstants::kDateTime> {
  // using base = BuilderBase<arrow::TimestampBuilder, DeephavenServerConstants::kDateTime>;

  // constructor with data type nanos
  ColumnBuilder() : BuilderBase<arrow::TimestampBuilder, DeephavenServerConstants::kDateTime>(
      std::make_shared<arrow::TimestampBuilder>(arrow::timestamp(arrow::TimeUnit::NANO, "UTC"),
          arrow::default_memory_pool())) {
  }

  void Append(const deephaven::dhcore::DateTime &value) {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Append(value.Nanos())));
  }
};

template<>
struct ColumnBuilder<deephaven::dhcore::LocalDate> : public BuilderBase<arrow::Date64Builder,
    DeephavenServerConstants::kLocalDate> {
  // using base = BuilderBase<arrow::Date64Builder, DeephavenServerConstants::kLocalDate>;

  // constructor with data type nanos
  ColumnBuilder() : BuilderBase<arrow::Date64Builder, DeephavenServerConstants::kLocalDate>(
      std::make_shared<arrow::Date64Builder>()) {
  }

  void Append(const deephaven::dhcore::DateTime &value) {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Append(value.Nanos())));
  }
};

template<>
struct ColumnBuilder<deephaven::dhcore::LocalTime> : public BuilderBase<arrow::Time64Builder,
    DeephavenServerConstants::kLocalTime> {
  ColumnBuilder() : BuilderBase<arrow::Time64Builder, DeephavenServerConstants::kLocalTime>(
      std::make_shared<arrow::Time64Builder>(arrow::time64(arrow::TimeUnit::NANO),
  arrow::default_memory_pool())) {

  }

  void Append(const deephaven::dhcore::LocalDate &value) {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Append(value.Millis())));
  }
};

template<arrow::TimeUnit::type UNIT>
struct ColumnBuilder<InternalDateTime<UNIT>> : public BuilderBase<arrow::TimestampBuilder,
    DeephavenServerConstants::kDateTime> {
  ColumnBuilder() : BuilderBase<arrow::TimestampBuilder, DeephavenServerConstants::kDateTime>(
      std::make_shared<arrow::TimestampBuilder>(arrow::timestamp(UNIT, "UTC"),
          arrow::default_memory_pool())) {
  }

  void Append(const InternalDateTime<UNIT> &value) {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Append(value.value_)));
  }
};

template<arrow::TimeUnit::type UNIT>
struct ColumnBuilder<InternalLocalTime<UNIT>> : public BuilderBase<arrow::Time64Builder,
    DeephavenServerConstants::kLocalTime> {
  ColumnBuilder() : BuilderBase<arrow::Time64Builder, DeephavenServerConstants::kLocalTime>(
      std::make_shared<arrow::Time64Builder>(arrow::time64(UNIT),
          arrow::default_memory_pool())) {
  }

  void Append(const InternalLocalTime<UNIT> &value) {
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder_->Append(value.value_)));
  }
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

  const char *GetDeephavenServerTypeName() {
    return inner_builder_.GetDeephavenServerTypeName();
  }

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

  const char *GetDeephavenServerTypeName() {
    // TODO(kosak)
    return "something.list.something";
  }

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
    auto array = cb.Finish();
    const auto *dh_type = cb.GetDeephavenServerTypeName();
    FinishAddColumn(std::move(name), std::move(array), dh_type);
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
  TableHandle MakeTable(const TableHandleManager &manager) const;

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
}  // namespace deephaven::client::utility
