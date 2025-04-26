/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/dhcore/utility/cython_support.h"

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>
#include "deephaven/dhcore/chunk/chunk.h"
#include "deephaven/dhcore/types.h"
#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/dhcore/column/column_source.h"
#include "deephaven/dhcore/column/array_column_source.h"
#include "deephaven/dhcore/container/container_util.h"
#include "deephaven/dhcore/container/row_sequence.h"

using deephaven::dhcore::chunk::BooleanChunk;
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
using deephaven::dhcore::column::BooleanArrayColumnSource;
using deephaven::dhcore::column::ColumnSource;
using deephaven::dhcore::column::ColumnSourceVisitor;
using deephaven::dhcore::column::ContainerArrayColumnSource;
using deephaven::dhcore::column::DateTimeArrayColumnSource;
using deephaven::dhcore::column::LocalDateArrayColumnSource;
using deephaven::dhcore::column::LocalTimeArrayColumnSource;
using deephaven::dhcore::column::StringArrayColumnSource;
using deephaven::dhcore::container::ContainerUtil;
using deephaven::dhcore::container::RowSequence;

namespace deephaven::dhcore::utility {
namespace {
void PopulateArrayFromPackedData(const uint8_t *src, bool *dest, size_t num_elements, bool invert);
void PopulateNullsFromDeephavenConvention(const int64_t *data_begin, bool *dest, size_t num_elements);
}  // namespace

std::shared_ptr<ColumnSource>
CythonSupport::CreateBooleanColumnSource(const uint8_t *data_begin, const uint8_t *data_end,
    const uint8_t *validity_begin, const uint8_t *validity_end, size_t num_elements) {
  auto elements = std::make_unique<bool[]>(num_elements);
  auto nulls = std::make_unique<bool[]>(num_elements);

  PopulateArrayFromPackedData(data_begin, elements.get(), num_elements, false);
  PopulateArrayFromPackedData(validity_begin, nulls.get(), num_elements, true);
  return BooleanArrayColumnSource::CreateFromArrays(ElementType::Of(ElementTypeId::kBool),
      std::move(elements), std::move(nulls), num_elements);
}

std::shared_ptr<ColumnSource>
CythonSupport::CreateStringColumnSource(const char *text_begin, const char *text_end,
    const uint32_t *offsets_begin, const uint32_t *offsets_end, const uint8_t *validity_begin,
    const uint8_t *validity_end, size_t num_elements) {
  auto elements = std::make_unique<std::string[]>(num_elements);
  auto nulls = std::make_unique<bool[]>(num_elements);

  const auto *current = text_begin;
  for (size_t i = 0; i != num_elements; ++i) {
    auto element_size = offsets_begin[i + 1] - offsets_begin[i];
    elements[i] = std::string(current, current + element_size);
    current += element_size;
  }
  PopulateArrayFromPackedData(validity_begin, nulls.get(), num_elements, true);
  return StringArrayColumnSource::CreateFromArrays(ElementType::Of(ElementTypeId::kString),
      std::move(elements), std::move(nulls), num_elements);
}

std::shared_ptr<ColumnSource>
CythonSupport::CreateDateTimeColumnSource(const int64_t *data_begin, const int64_t *data_end,
    const uint8_t *validity_begin, const uint8_t *validity_end, size_t num_elements) {
  auto elements = std::make_unique<DateTime[]>(num_elements);
  auto nulls = std::make_unique<bool[]>(num_elements);

  for (size_t i = 0; i != num_elements; ++i) {
    elements[i] = DateTime::FromNanos(data_begin[i]);
  }
  PopulateNullsFromDeephavenConvention(data_begin, nulls.get(), num_elements);
  return DateTimeArrayColumnSource::CreateFromArrays(ElementType::Of(ElementTypeId::kTimestamp),
      std::move(elements), std::move(nulls), num_elements);
}

std::shared_ptr<ColumnSource>
CythonSupport::CreateLocalDateColumnSource(const int64_t *data_begin, const int64_t *data_end,
    const uint8_t *validity_begin, const uint8_t *validity_end, size_t num_elements) {
  auto elements = std::make_unique<LocalDate[]>(num_elements);
  auto nulls = std::make_unique<bool[]>(num_elements);

  for (size_t i = 0; i != num_elements; ++i) {
    elements[i] = LocalDate::FromMillis(data_begin[i]);
  }
  PopulateNullsFromDeephavenConvention(data_begin, nulls.get(), num_elements);
  return LocalDateArrayColumnSource::CreateFromArrays(ElementType::Of(ElementTypeId::kLocalDate),
      std::move(elements), std::move(nulls), num_elements);
}

std::shared_ptr<ColumnSource>
CythonSupport::CreateLocalTimeColumnSource(const int64_t *data_begin, const int64_t *data_end,
    const uint8_t *validity_begin, const uint8_t *validity_end, size_t num_elements) {
  auto elements = std::make_unique<LocalTime[]>(num_elements);
  auto nulls = std::make_unique<bool[]>(num_elements);

  for (size_t i = 0; i != num_elements; ++i) {
    elements[i] = LocalTime::FromNanos(data_begin[i]);
  }
  PopulateNullsFromDeephavenConvention(data_begin, nulls.get(), num_elements);
  return LocalTimeArrayColumnSource::CreateFromArrays(ElementType::Of(ElementTypeId::kLocalTime),
      std::move(elements), std::move(nulls), num_elements);
}

ElementTypeId::Enum CythonSupport::GetElementTypeId(const ColumnSource &column_source) {
  const auto &element_type = column_source.GetElementType();
  if (element_type.ListDepth() != 0) {
    const char *message = "GetElementTypeId does not support non-zero list depth";
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
  }
  return element_type.Id();
}

namespace {
struct CreateContainerVisitor final : ColumnSourceVisitor {
  void Visit(const column::CharColumnSource &source) override {
    VisitHelper<char16_t, CharChunk>(ElementTypeId::kChar);
  }

  void Visit(const column::Int8ColumnSource &source) override {
    VisitHelper<int8_t, Int8Chunk>(ElementTypeId::kInt8);
  }

  void Visit(const column::Int16ColumnSource &source) override {
    VisitHelper<int16_t, Int16Chunk>(ElementTypeId::kInt16);
  }

  void Visit(const column::Int32ColumnSource &source) override {
    VisitHelper<int32_t, Int32Chunk>(ElementTypeId::kInt32);
  }

  void Visit(const column::Int64ColumnSource &source) override {
    VisitHelper<int64_t, Int64Chunk>(ElementTypeId::kInt64);
  }

  void Visit(const column::FloatColumnSource &source) override {
    VisitHelper<float, FloatChunk>(ElementTypeId::kFloat);
  }

  void Visit(const column::DoubleColumnSource &source) override {
    VisitHelper<double, DoubleChunk>(ElementTypeId::kDouble);
  }

  void Visit(const column::BooleanColumnSource &source) override {
    VisitHelper<bool, BooleanChunk>(ElementTypeId::kBool);
  }

  void Visit(const column::StringColumnSource &source) override {
    VisitHelper<std::string, StringChunk>(ElementTypeId::kString);
  }

  void Visit(const column::DateTimeColumnSource &source) override {
    VisitHelper<DateTime, DateTimeChunk>(ElementTypeId::kTimestamp);
  }

  void Visit(const column::LocalDateColumnSource &source) override {
    VisitHelper<LocalDate, LocalDateChunk>(ElementTypeId::kLocalDate);
  }

  void Visit(const column::LocalTimeColumnSource &source) override {
    VisitHelper<LocalTime, LocalTimeChunk>(ElementTypeId::kLocalTime);
  }

  void Visit(const column::ContainerBaseColumnSource &source) final {
    const char *message = "Nested containers are not supported";
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
  }

  template<typename TElement, typename TChunk>
  void VisitHelper(ElementTypeId::Enum element_type_id) {
    result_ = ContainerUtil::Inflate<TElement, TChunk>(ElementType::Of(element_type_id),
        *data_, data_size_, slice_lengths_);
  }

  std::shared_ptr<ColumnSource> data_;
  size_t data_size_ = 0;
  std::vector<std::optional<size_t>> slice_lengths_;
  std::shared_ptr<ContainerArrayColumnSource> result_;
};
}  // namespace

std::shared_ptr<ColumnSource>
CythonSupport::CreateContainerColumnSource(std::shared_ptr<ColumnSource> data, size_t data_size,
    std::shared_ptr<ColumnSource> lengths, size_t lengths_size) {
  // assume that lengths has underlying type int32
  // do a visitor for the data
  // which
  // makes a chunk


}

namespace {
void PopulateArrayFromPackedData(const uint8_t *src, bool *dest, size_t num_elements, bool invert) {
  if (src == nullptr) {
    std::fill(dest, dest + num_elements, false);
    return;
  }
  uint32_t src_mask = 1;
  while (num_elements != 0) {
    auto value = static_cast<bool>(*src & src_mask) ^ invert;
    *dest++ = static_cast<bool>(value);
    src_mask <<= 1;
    if (src_mask == 0x100) {
      src_mask = 1;
      ++src;
    }
    --num_elements;
  }
}

void PopulateNullsFromDeephavenConvention(const int64_t *data_begin, bool *dest, size_t num_elements) {
  for (size_t i = 0; i != num_elements; ++i) {
    dest[i] = data_begin[i] == DeephavenConstants::kNullLong;
  }
}
}  // namespace
}  // namespace deephaven::dhcore::utility
