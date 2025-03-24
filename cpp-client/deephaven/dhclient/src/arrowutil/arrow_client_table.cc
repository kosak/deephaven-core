/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/arrowutil/arrow_column_source.h"
#include "deephaven/client/arrowutil/arrow_client_table.h"
#include "deephaven/client/utility/arrow_util.h"
#include "deephaven/dhcore/column/array_column_source.h"
#include "deephaven/dhcore/container/container.h"
#include "deephaven/dhcore/types.h"
#include "arrow/scalar.h"

using deephaven::client::utility::ArrowUtil;
using deephaven::client::utility::OkOrThrow;
using deephaven::client::utility::ValueOrThrow;
using deephaven::client::arrowutil::BooleanArrowColumnSource;
using deephaven::client::arrowutil::CharArrowColumnSource;
using deephaven::client::arrowutil::DateTimeArrowColumnSource;
using deephaven::client::arrowutil::DoubleArrowColumnSource;
using deephaven::client::arrowutil::FloatArrowColumnSource;
using deephaven::client::arrowutil::Int8ArrowColumnSource;
using deephaven::client::arrowutil::Int16ArrowColumnSource;
using deephaven::client::arrowutil::Int32ArrowColumnSource;
using deephaven::client::arrowutil::Int64ArrowColumnSource;
using deephaven::client::arrowutil::StringArrowColumnSource;
using deephaven::dhcore::chunk::BooleanChunk;
using deephaven::dhcore::chunk::Chunk;
using deephaven::dhcore::chunk::CharChunk;
using deephaven::dhcore::chunk::DateTimeChunk;
using deephaven::dhcore::chunk::FloatChunk;
using deephaven::dhcore::chunk::DoubleChunk;
using deephaven::dhcore::chunk::Int8Chunk;
using deephaven::dhcore::chunk::Int16Chunk;
using deephaven::dhcore::chunk::Int32Chunk;
using deephaven::dhcore::chunk::Int64Chunk;
using deephaven::dhcore::chunk::StringChunk;
using deephaven::dhcore::chunk::UInt64Chunk;
using deephaven::dhcore::clienttable::ClientTable;
using deephaven::dhcore::column::BooleanColumnSource;
using deephaven::dhcore::column::CharColumnSource;
using deephaven::dhcore::column::ColumnSource;
using deephaven::dhcore::column::ContainerArrayColumnSource;
using deephaven::dhcore::column::DoubleColumnSource;
using deephaven::dhcore::column::FloatColumnSource;
using deephaven::dhcore::column::Int8ColumnSource;
using deephaven::dhcore::column::Int16ColumnSource;
using deephaven::dhcore::column::Int32ColumnSource;
using deephaven::dhcore::column::Int64ColumnSource;
using deephaven::dhcore::column::ColumnSourceVisitor;
using deephaven::dhcore::column::StringColumnSource;
using deephaven::dhcore::container::Container;
using deephaven::dhcore::container::ContainerBase;
using deephaven::dhcore::container::RowSequence;
using deephaven::dhcore::DateTime;
using deephaven::dhcore::DeephavenTraits;
using deephaven::dhcore::utility::demangle;
using deephaven::dhcore::utility::MakeReservedVector;
using deephaven::dhcore::utility::VerboseCast;

namespace deephaven::client::arrowutil {

namespace {
std::shared_ptr<ColumnSource> MakeColumnSource(const arrow::ChunkedArray &array);
}  // namespace

std::shared_ptr<ClientTable> ArrowClientTable::Create(std::shared_ptr<arrow::Table> arrow_table) {
  auto schema = ArrowUtil::MakeDeephavenSchema(*arrow_table->schema());
  auto row_sequence = RowSequence::CreateSequential(0, arrow_table->num_rows());

  auto column_sources = MakeReservedVector<std::shared_ptr<ColumnSource>>(arrow_table->num_columns());
  for (const auto &chunked_array : arrow_table->columns()) {
    column_sources.push_back(MakeColumnSource(*chunked_array));
  }

  return std::make_shared<ArrowClientTable>(Private(), std::move(arrow_table),
      std::move(schema), std::move(row_sequence), std::move(column_sources));
}

ArrowClientTable::ArrowClientTable(deephaven::client::arrowutil::ArrowClientTable::Private,
    std::shared_ptr<arrow::Table> arrow_table, std::shared_ptr<SchemaType> schema,
    std::shared_ptr<RowSequence> row_sequence,
    std::vector<std::shared_ptr<ColumnSource>> column_sources) :
    arrow_table_(std::move(arrow_table)), schema_(std::move(schema)),
    row_sequence_(std::move(row_sequence)), column_sources_(std::move(column_sources)) {}
ArrowClientTable::ArrowClientTable(ArrowClientTable &&other) noexcept = default;
ArrowClientTable &ArrowClientTable::operator=(ArrowClientTable &&other) noexcept = default;
ArrowClientTable::~ArrowClientTable() = default;

std::shared_ptr<ColumnSource> ArrowClientTable::GetColumn(size_t column_index) const {
  if (column_index >= column_sources_.size()) {
    auto message = fmt::format("column_index ({}) >= num columns ({})", column_index,
        column_sources_.size());
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
  }
  return column_sources_[column_index];
}

namespace {
template<typename TArrowArray>
std::vector<std::shared_ptr<TArrowArray>> DowncastChunks(const arrow::ChunkedArray &chunked_array) {
  auto downcasted = MakeReservedVector<std::shared_ptr<TArrowArray>>(chunked_array.num_chunks());
  for (const auto &vec : chunked_array.chunks()) {
    auto dest = std::dynamic_pointer_cast<TArrowArray>(vec);
    if (dest == nullptr) {
      const auto &deref_vec = *vec;
      auto message = fmt::format("can't cast {} to {}",
          demangle(typeid(deref_vec).name()),
          demangle(typeid(TArrowArray).name()));
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }
    downcasted.push_back(std::move(dest));
  }
  return downcasted;
}


struct ZamboniCopier final : public arrow::TypeVisitor {
  ZamboniCopier(std::shared_ptr<ColumnSource> flattened_elements,
    std::unique_ptr<size_t[]> slice_lengths,
    std::unique_ptr<bool[]> slice_nulls,
    size_t num_slices,
    size_t flattened_size) :
    flattened_elements_(std::move(flattened_elements)),
    slice_lengths_(std::move(slice_lengths)),
    slice_nulls_(std::move(slice_nulls)),
    num_slices_(num_slices),
    flattened_size_(flattened_size) {}

  arrow::Status Visit(const arrow::Int32Type &/*type*/) final {
    std::shared_ptr<int32_t[]> flattened_data(new int32_t[flattened_size_]);
    std::shared_ptr<bool[]> flattened_nulls(new bool[flattened_size_]);

    auto flattened_data_chunk = Int32Chunk::CreateView(flattened_data.get(), flattened_size_);
    auto flattened_nulls_chunk = BooleanChunk::CreateView(flattened_nulls.get(), flattened_size_);
    auto rs = RowSequence::CreateSequential(0, flattened_size_);
    flattened_elements_->FillChunk(*rs, &flattened_data_chunk, &flattened_nulls_chunk);

    auto slices = std::make_unique<std::shared_ptr<ContainerBase>[]>(num_slices_);

    size_t slice_offset = 0;
    for (size_t i = 0; i != num_slices_; ++i) {
      auto *slice_data_start = flattened_data.get() + slice_offset;
      auto *slice_null_start = flattened_nulls.get() + slice_offset;

      // Make shared pointers from these slice pointers that share the lifetime of the
      // original shared pointers
      std::shared_ptr<int32_t[]> slice_data_start_sp(flattened_data, slice_data_start);
      std::shared_ptr<bool[]> slice_null_start_sp(flattened_nulls, slice_null_start);

      auto slice_size = slice_lengths_[i];
      auto slice = Container<int32_t>::Create(std::move(slice_data_start_sp),
          std::move(slice_null_start_sp), slice_size);
      slices[i] = std::move(slice);
      slice_offset += slice_size;
    }

    result_ = ContainerArrayColumnSource::CreateFromArrays(
        std::move(slices), std::move(slice_nulls_), num_slices_);
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::StringType &/*type*/) final {
    std::shared_ptr<std::string[]> flattened_data(new std::string[flattened_size_]);
    std::shared_ptr<bool[]> flattened_nulls(new bool[flattened_size_]);

    auto flattened_data_chunk = StringChunk::CreateView(flattened_data.get(), flattened_size_);
    auto flattened_nulls_chunk = BooleanChunk::CreateView(flattened_nulls.get(), flattened_size_);
    auto rs = RowSequence::CreateSequential(0, flattened_size_);
    flattened_elements_->FillChunk(*rs, &flattened_data_chunk, &flattened_nulls_chunk);

    auto slices = std::make_unique<std::shared_ptr<ContainerBase>[]>(num_slices_);

    size_t slice_offset = 0;
    for (size_t i = 0; i != num_slices_; ++i) {
      auto *slice_data_start = flattened_data.get() + slice_offset;
      auto *slice_null_start = flattened_nulls.get() + slice_offset;

      // Make shared pointers from these slice pointers that share the lifetime of the
      // original shared pointers
      std::shared_ptr<std::string[]> slice_data_start_sp(flattened_data, slice_data_start);
      std::shared_ptr<bool[]> slice_null_start_sp(flattened_nulls, slice_null_start);

      auto slice_size = slice_lengths_[i];
      auto slice = Container<std::string>::Create(std::move(slice_data_start_sp),
          std::move(slice_null_start_sp), slice_size);
      slices[i] = std::move(slice);
      slice_offset += slice_size;
    }

    result_ = ContainerArrayColumnSource::CreateFromArrays(
        std::move(slices), std::move(slice_nulls_), num_slices_);
    return arrow::Status::OK();
  }

  std::shared_ptr<ColumnSource> flattened_elements_;
  std::unique_ptr<size_t[]> slice_lengths_;
  std::unique_ptr<bool[]> slice_nulls_;
  size_t num_slices_ = 0;
  size_t flattened_size_ = 0;
  std::shared_ptr<ColumnSource> result_;
};

struct Visitor final : public arrow::TypeVisitor {
  explicit Visitor(const arrow::ChunkedArray &chunked_array) : chunked_array_(chunked_array) {}

  arrow::Status Visit(const arrow::UInt16Type &/*type*/) final {
    auto arrays = DowncastChunks<arrow::UInt16Array>(chunked_array_);
    result_ = CharArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int8Type &/*type*/) final {
    auto arrays = DowncastChunks<arrow::Int8Array>(chunked_array_);
    result_ = Int8ArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int16Type &/*type*/) final {
    auto arrays = DowncastChunks<arrow::Int16Array>(chunked_array_);
    result_ = Int16ArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int32Type &/*type*/) final {
    auto arrays = DowncastChunks<arrow::Int32Array>(chunked_array_);
    result_ = Int32ArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int64Type &/*type*/) final {
    auto arrays = DowncastChunks<arrow::Int64Array>(chunked_array_);
    result_ = Int64ArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::FloatType &/*type*/) final {
    auto arrays = DowncastChunks<arrow::FloatArray>(chunked_array_);
    result_ = FloatArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::DoubleType &/*type*/) final {
    auto arrays = DowncastChunks<arrow::DoubleArray>(chunked_array_);
    result_ = DoubleArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::BooleanType &/*type*/) final {
    auto arrays = DowncastChunks<arrow::BooleanArray>(chunked_array_);
    result_ = BooleanArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::StringType &/*type*/) final {
    auto arrays = DowncastChunks<arrow::StringArray>(chunked_array_);
    result_ = StringArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::TimestampType &/*type*/) final {
    auto arrays = DowncastChunks<arrow::TimestampArray>(chunked_array_);
    result_ = DateTimeArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Date64Type &/*type*/) final {
    auto arrays = DowncastChunks<arrow::Date64Array>(chunked_array_);
    result_ = LocalDateArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Time64Type &/*type*/) final {
    auto arrays = DowncastChunks<arrow::Time64Array>(chunked_array_);
    result_ = LocalTimeArrowColumnSource::OfArrowArrayVec(std::move(arrays));
    return arrow::Status::OK();
  }

  /**
   * When the element is a list, we need to go one level deeper to decode it.
   */
  arrow::Status Visit(const arrow::ListType &type) final {
    auto chunked_listarrays = DowncastChunks<arrow::ListArray>(chunked_array_);

    // 1. extract offsets
    // 2. use recursion to create a column set of values
    // 3. use rowset operations to extract an array from that column source (hacky)
    // 4. return a ContainerColumnSource of that array
    auto flattened_chunks = MakeReservedVector<std::shared_ptr<arrow::Array>>(
        chunked_listarrays.size());
    size_t num_slices = 0;
    size_t flattened_size = 0;
    for (const auto &la: chunked_listarrays) {
      flattened_chunks.push_back(la->values());
      num_slices += la->length();
      flattened_size += la->values()->length();
    }
    auto flattened_chunked_array = ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(
        arrow::ChunkedArray::Make(std::move(flattened_chunks), type.value_type())));
    auto flattened_elements = MakeColumnSource(*flattened_chunked_array);

    // We have a single column source with all the flattened data in it. Now we have to
    // recover the offset, length, and nullness of each slice. This is unusually annoying because
    // our input was a ChunkedArray, not just an array.

    auto slice_lengths = std::make_unique<size_t[]>(num_slices);
    auto slice_nulls = std::make_unique<bool[]>(num_slices);
    size_t next_index = 0;
    for (const auto &la : chunked_listarrays) {
      for (int64_t i = 0; i != la->length(); ++i, ++next_index) {
        slice_lengths[next_index] = la->value_length(i);
        slice_nulls[next_index] = la->IsNull(i);
      }
    }
    if (next_index != num_slices) {
      auto message = fmt::format("Programming error: {} != {}", next_index, num_slices);
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }

    ZamboniCopier copier(std::move(flattened_elements), std::move(slice_lengths),
        std::move(slice_nulls), num_slices, flattened_size);
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(type.value_type()->Accept(&copier)));
    result_ = std::move(copier.result_);
    return arrow::Status::OK();
  }

  const arrow::ChunkedArray &chunked_array_;
  std::shared_ptr<ColumnSource> result_;
};

std::shared_ptr<ColumnSource> MakeColumnSource(const arrow::ChunkedArray &chunked_array) {
  Visitor visitor(chunked_array);
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(chunked_array.type()->Accept(&visitor)));
  return std::move(visitor.result_);
}
}  // namespace
}  // namespace deephaven::client::arrowutil
