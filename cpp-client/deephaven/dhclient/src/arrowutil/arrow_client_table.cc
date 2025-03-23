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
std::shared_ptr<ColumnSource> MakeColumnSource(
    const std::vector<std::shared_ptr<arrow::Array>> &array_chunks, const arrow::DataType &type);
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

struct ElementIsListVisitor final : public arrow::TypeVisitor {
  explicit ElementIsListVisitor(std::vector<std::shared_ptr<arrow::ListArray>> list_arrays) :
      list_arrays_(std::move(list_arrays)) {}

  arrow::Status Visit(const arrow::StringType &/*type*/) final {
    int64_t total_rows = 0;
    for (const auto &la : list_arrays_) {
      total_rows += la->length();
    }

    auto all_flattened_elements =
        MakeReservedVector<std::vector<std::optional<std::string>>>(list_arrays_.size());

    {
      for (const auto &la: list_arrays_) {
        auto next_slice_values = std::dynamic_pointer_cast<arrow::StringArray>(la->values());
        auto next_slice_size = la->values()->length();

        auto as_cs = StringArrowColumnSource::OfArrowArray(std::move(next_slice_values));

        auto rs = RowSequence::CreateSequential(0, next_slice_size);
        auto element_chunk = StringChunk::Create(next_slice_size);
        auto element_nulls = BooleanChunk::Create(next_slice_size);
        as_cs->FillChunk(*rs, &element_chunk, &element_nulls);

        auto flattened_elements = MakeReservedVector<std::optional<std::string>>(next_slice_size);
        for (int64_t i = 0; i != next_slice_size; ++i) {
          if (element_nulls[i]) {
            // The empty optional, signifying null
            flattened_elements.emplace_back();
            continue;
          }
          flattened_elements.emplace_back(std::move(element_chunk.data()[i]));
        }

        all_flattened_elements.push_back(std::move(flattened_elements));
      }
    }

    for (const auto &outer : all_flattened_elements) {
      std::cout << "NEXT OUTER\n";
      for (const auto &inner : outer) {
        std::cout << *inner << '\n';
      }
    }

//    for (auto i = 0; i != num_flattened_elements; ++i) {
//    }
//    std::cout << "Right? Wrong? Who can say\n";
//
//    auto result_elements = std::make_unique<std::shared_ptr<ContainerBase>[]>(num_rows);
//    auto result_nulls = std::make_unique<bool[]>(num_rows);
//
//    int64_t next_row = 0;
//    for (const auto &la : list_arrays_) {
//      for (int64_t i = 0; i != la->length(); ++i, ++next_row) {
//        if (la->IsNull(i)) {
//          result_elements[next_row] = nullptr;
//          result_nulls[next_row] = true;
//          continue;
//        }
//        auto blah = la->value_offset(i);
//        auto blah2 = la->value_length(i);
//
//
//        auto slice = la->value_slice(i);
//        auto slice_length = slice->length();
//        auto as_element_array = std::dynamic_pointer_cast<arrow::StringArray>(slice);
//
//        // Use ColumnSource as an intermediate data structure to do any necessary conversions
//        // This is a bit much. We could take another pass at this to do it more efficiently.
//        auto as_cs = StringArrowColumnSource::OfArrowArray(std::move(as_element_array));
//
//        auto rs = RowSequence::CreateSequential(0, slice_length);
//        auto element_chunk = StringChunk::Create(slice_length);
//        auto element_nulls = BooleanChunk::Create(slice_length);
//        as_cs->FillChunk(*rs, &element_chunk, &element_nulls);
//
//
//        elements[next_index] = std::move(as_cs);
//        nulls[next_index] = false;
//      }
//    }
//    result_ = ContainerArrayColumnSource::CreateFromArrays(std::move(elements),
//        std::move(nulls), total_size);
    return arrow::Status::OK();
  }

  std::vector<std::shared_ptr<arrow::ListArray>> list_arrays_;
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
    auto arrays = DowncastChunks<arrow::ListArray>(chunked_array_);

    std::vector<std::shared_ptr<arrow::ListArray>> skunked_array;

    // 1. extract offsets
    // 2. use recursion to create a column set of values
    // 3. use rowset operations to extract an array from that column source (hacky)
    // 4. return a ContainerColumnSource of that array

    auto inner_chunks = MakeReservedVector<std::shared_ptr<arrow::Array>>(skunked_array.size());
    for (const auto &la : skunked_array) {
      inner_chunks.push_back(la->values());
    }

    auto inner_cs = MakeColumnSource(inner_chunks, *type.value_type());

    // this is us wishing we knew our size
    auto size = 11;
    ZamboniCopier copier(std::move(inner_cs), size);
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(type.value_type()->Accept(&copier)));
    result_ = std::move(copier.result);
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

std::shared_ptr<ColumnSource> MakeColumnSource(
    const std::vector<std::shared_ptr<arrow::Array>> &array_chunks, const arrow::DataType &type) {
  Visitor visitor(array_chunks);
  OkOrThrow(DEEPHAVEN_LOCATION_EXPR(type.Accept(&visitor)));
  return std::move(visitor.result_);
}
}  // namespace
}  // namespace deephaven::client::arrowutil
