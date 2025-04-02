/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/arrowutil/arrow_array_converter.h"

#include <memory>
#include <utility>
#include <arrow/visitor.h>
#include <arrow/array/array_base.h>
#include <arrow/array/array_primitive.h>
#include "deephaven/client/arrowutil/arrow_column_source.h"
#include "deephaven/client/utility/arrow_util.h"
#include "deephaven/dhcore/column/column_source.h"
#include "deephaven/dhcore/utility/utility.h"

namespace deephaven::client::arrowutil {
using deephaven::client::utility::OkOrThrow;
using deephaven::dhcore::column::ColumnSource;
using deephaven::dhcore::utility::VerboseCast;

namespace {
struct ArrayToColumnSourceVisitor final : public arrow::ArrayVisitor {
  explicit ArrayToColumnSourceVisitor(const std::shared_ptr<arrow::Array> &array) :
    array_(array) {}

  arrow::Status Visit(const arrow::Int8Array &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::Int8Array>(array_);
    result_ = Int8ArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int16Array &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::Int16Array>(array_);
    result_ = Int16ArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int32Array &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::Int32Array>(array_);
    result_ = Int32ArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int64Array &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::Int64Array>(array_);
    result_ = Int64ArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::FloatArray &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::FloatArray>(array_);
    result_ = FloatArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::DoubleArray &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::DoubleArray>(array_);
    result_ = DoubleArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::BooleanArray &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::BooleanArray>(array_);
    result_ = BooleanArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::UInt16Array &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::UInt16Array>(array_);
    result_ = CharArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::StringArray &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::StringArray>(array_);
    result_ = StringArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::TimestampArray &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::TimestampArray>(array_);
    result_ = DateTimeArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Date64Array &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::Date64Array>(array_);
    result_ = LocalDateArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Time64Array &/*array*/) final {
    auto typed_array = std::dynamic_pointer_cast<arrow::Time64Array>(array_);
    result_ = LocalTimeArrowColumnSource::OfArrowArray(std::move(typed_array));
    return arrow::Status::OK();
  }

  const std::shared_ptr<arrow::Array> &array_;
  std::shared_ptr<ColumnSource> result_;
};
}  // namespace

std::shared_ptr<ColumnSource> ArrowArrayConverter::ArrayToColumnSource(const arrow::Array &array) {
  const auto *list_array = VerboseCast<const arrow::ListArray *>(DEEPHAVEN_LOCATION_EXPR(&array));

  if (list_array->length() != 1) {
    auto message = fmt::format("Expected array of length 1, got {}", array.length());
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
  }

  const auto list_element = list_array->GetScalar(0).ValueOrDie();
  const auto *list_scalar = VerboseCast<const arrow::ListScalar *>(
      DEEPHAVEN_LOCATION_EXPR(list_element.get()));
  const auto &list_scalar_value = list_scalar->value;

  ArrayToColumnSourceVisitor v(list_scalar_value);
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(list_scalar_value->Accept(&v)));
  return {std::move(v.result_), static_cast<size_t>(list_scalar_value->length())};
}

std::shared_ptr<ColumnSource> ArrowArrayConverter::ArrayToColumnSource(
    const std::shared_ptr<arrow::Array> &array) {
  ArrayToColumnSourceVisitor v(array);
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(array->Accept(&v)));
  return std::move(v.result_);
}
}  // namespace deephaven::client::arrowutil
