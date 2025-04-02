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
class Reconstituter final : public arrow::TypeVisitor {
public:
  Reconstituter(std::shared_ptr<ColumnSource> flattened_elements,
      std::unique_ptr<size_t[]> slice_lengths,
      std::unique_ptr<bool[]> slice_nulls,
      size_t num_slices,
      size_t flattened_size) :
      flattened_elements_(std::move(flattened_elements)),
      slice_lengths_(std::move(slice_lengths)),
      slice_nulls_(std::move(slice_nulls)),
      num_slices_(num_slices),
      flattened_size_(flattened_size),
      flattened_nulls_(new bool[flattened_size_]),
      flattened_nulls_chunk_(BooleanChunk::CreateView(flattened_nulls_.get(), flattened_size_)),
      rowSequence_(RowSequence::CreateSequential(0, flattened_size_)),
      slices_(std::make_unique<std::shared_ptr<ContainerBase>[]>(num_slices_)) {
  }

  ~Reconstituter() final = default;

  std::shared_ptr<ContainerArrayColumnSource> MakeResult() {
    return ContainerArrayColumnSource::CreateFromArrays(
        std::move(slices_), std::move(slice_nulls_), num_slices_);
  }

  arrow::Status Visit(const arrow::Int32Type &/*type*/) final {
    return VisitHelper<int32_t, Int32Chunk>();
  }

  // static_assert(false, "do all the visitors here");

  arrow::Status Visit(const arrow::StringType &/*type*/) final {
    return VisitHelper<std::string, StringChunk>();
  }

private:
  template<typename TElement, typename TChunk>
  arrow::Status VisitHelper() {
    std::shared_ptr<TElement[]> flattened_data(new TElement[flattened_size_]);
    auto flattened_data_chunk = TChunk::CreateView(flattened_data.get(), flattened_size_);
    flattened_elements_->FillChunk(*rowSequence_, &flattened_data_chunk, &flattened_nulls_chunk_);

    size_t slice_offset = 0;
    for (size_t i = 0; i != num_slices_; ++i) {
      auto *slice_data_start = flattened_data.get() + slice_offset;
      auto *slice_null_start = flattened_nulls_.get() + slice_offset;

      // Make shared pointers from these slice pointers that share the lifetime of the
      // original shared pointers
      std::shared_ptr<TElement[]> slice_data_start_sp(flattened_data, slice_data_start);
      std::shared_ptr<bool[]> slice_null_start_sp(flattened_nulls_, slice_null_start);

      auto slice_size = slice_lengths_[i];
      auto slice = Container<TElement>::Create(std::move(slice_data_start_sp),
          std::move(slice_null_start_sp), slice_size);
      slices_[i] = std::move(slice);
      slice_offset += slice_size;
    }
    return arrow::Status::OK();
  }

  std::shared_ptr<ColumnSource> flattened_elements_;
  std::unique_ptr<size_t[]> slice_lengths_;
  std::unique_ptr<bool[]> slice_nulls_;
  size_t num_slices_ = 0;
  size_t flattened_size_ = 0;
  std::shared_ptr<bool[]> flattened_nulls_;
  BooleanChunk flattened_nulls_chunk_;
  std::shared_ptr<RowSequence> rowSequence_;
  std::unique_ptr<std::shared_ptr<ContainerBase>[]> slices_;
};

struct ChunkedArrayToColumnSourceVisitor final : public arrow::TypeVisitor {
  explicit ChunkedArrayToColumnSourceVisitor(const arrow::ChunkedArray &chunked_array) :
    chunked_array_(chunked_array) {}

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
   * When the element is a list, we use recursion to extract the flattened elements of the list into
   * a single column source. Then e extract the data out of that column source into a single pair
   * of arrays (one for the flattened data, and one for the flattened null flags), and then we
   * reconstitute the 2D list structure as a ColumnSource<shared_ptr<ContainerBase>>, where the
   * shared_ptr<ContainerBase> has the appropriate dynamic type.
   *
   * Example
   * Input is ListArray:
   *   [a, b, c]
   *   null
   *   []
   *   [d, e, f, null, g]
   *
   * It has 4 slices.
   * When it is flattened, it looks like [a, b, c, d, e, f, null, g]. There are 8 elements in the
   * flattened data. Note: that throughout this discussion it will be important to recognize the
   * difference between a slice that is null (the second slice, above), a slice that is empty
   * (the third slice above), and a slice that is not null but contains a null element (the fourth
   * element of the fourth slice above).
   *
   * We use recursion to create the flattened data [a, b, c, d, e, f, null, g] as a ColumnSource
   * (of the appropriate dynamic type). This ColumnSource is only a very temporary holding area,
   * because we immediately copy all the data back out of it. We do this as a convenience mainly
   * because the ColumnSource has knowledge about data type conversions and Deephaven null
   * conventions. In an alternate implementation we might be able to copy data directly from Arrow
   * to the two target arrays, but that would require some refactoring of our code.
   *
   * Anyway, next we make an array of slice lengths and slice null flags. These will be inputs to
   * the Reconstituter. In our example these arrays are of size 4 and contain:
   *   lengths: [3, 0, 0, 5]  [bookmark #1]
   *   null flags: [false, true, false, false]   [bookmark #2]
   *
   *  The first 0 in lengths is a "dontcare" because the element itself is null.
   *  The second 0 is an actual zero in the sense that it represents a list of length 0 (not a null
   *  entry)
   *
   * We then pass these arrays to the Reconstituter to finish the job of reconstituting a
   * ColumnSource<shared_ptr<ContainerBase>> from these arrays.
   *
   * Inside the Reconstituter, we make two arrays for the flattened data and the flattened null
   * flags. We use ColumnSource::FillChunk to populate these arrays. In our example these arrays
   * are of size 8 and contain:
   *   data: [a, b, c, d, e, f, null, g]
   *   nulls: [false, false, false, false, false, false, true, false]
   *
   * These arrays are owned by shared pointers because, when we are done, there will be multiple
   * Container<T> objects that point to (the interior) of these arrays and share their lifetime.
   *
   * Then the Reconstituter goes to work at recovering the original shape of the ListArray slices.
   * To do this, it uses the flattened data we just obtained, combined with the original slice
   * lengths (bookmark #1) and slice null flags (bookmark #2) that were passed in.
   *
   *   con0: size=3, data = [a, b, c], nulls=[false, false, false].
   *         It points into the above data and nulls arrays at offset 0.
   *   con1: size = 0, data = [], nulls = [].  This entry is null and so it doesn't matter where its
   *         data points to
   *   con2: size = 0, data = [], nulls = [].  This entry is of length zero, so for different
   *         reasons it also doesn't matter where its data points to
   *   con3: size = 5, data = [d, e, f, null, g], nulls = [false, false, false, true, false]
   *         It points into the above data and nulls arrays at offset 3.
   *
   * We arrange these container objects into an array of size 4 of shared_ptr<ContainerBase>
   *
   * The Reconstituter also needs a null flags array of size 4 to work in tandem with this
   * shared_ptr array. But the Reconstituter already have this null array; it was passed in as an
   * input to the Reconstituter (see bookmark #2). In this example it is:
   * [false, true, false, false]
   *
   * We provide these two arrays to ContainerArrayColumnSource::CreateFromArrays() and we are
   * done.
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

    Reconstituter reconstituter(std::move(flattened_elements), std::move(slice_lengths),
        std::move(slice_nulls), num_slices, flattened_size);
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(type.value_type()->Accept(&reconstituter)));
    result_ = reconstituter.MakeResult();
    return arrow::Status::OK();
  }

  const arrow::ChunkedArray &chunked_array_;
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
