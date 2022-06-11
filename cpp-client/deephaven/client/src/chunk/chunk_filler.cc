#include "deephaven/client/chunk/chunk_filler.h"

#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/impl/util.h"
#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::okOrThrow;
using deephaven::client::utility::stringf;
using deephaven::client::utility::verboseCast;
using deephaven::client::container::RowSequence;

namespace deephaven::client::chunk {
namespace {
struct Visitor final : arrow::ArrayVisitor {
  Visitor(const RowSequence &keys, AnyChunk *const dest) : keys_(keys), dest_(dest) {}

  arrow::Status Visit(const arrow::Int32Array &array) final {
    return fillNumericChunk<int32_t>(array);
  }

  arrow::Status Visit(const arrow::Int64Array &array) final {
    return fillNumericChunk<int64_t>(array);
  }

  arrow::Status Visit(const arrow::UInt64Array &array) final {
    return fillNumericChunk<uint64_t>(array);
  }

  arrow::Status Visit(const arrow::DoubleArray &array) final {
    return fillNumericChunk<double>(array);
  }

  template<typename T, typename TArrowAray>
  arrow::Status fillNumericChunk(const TArrowAray &array) {
    auto *typedDest = &dest_->template get<NumericChunk<T>>();
    checkSize(typedDest->size());
    size_t destIndex = 0;
    auto copyChunk = [&destIndex, &array, typedDest](uint64_t begin, uint64_t end) {
      for (auto current = begin; current != end; ++current) {
        auto val = array[(int64_t)current];
        if (!val.has_value()) {
          throw std::runtime_error(stringf("%o: Not handling nulls yet", __PRETTY_FUNCTION__));
        }
        typedDest->data()[destIndex] = *val;
        ++destIndex;
      }
    };
    keys_.forEachChunk(copyChunk);
    return arrow::Status::OK();
  }

  void checkSize(size_t destSize) {
    if (destSize < keys_.size()) {
      throw std::runtime_error(stringf("destSize < keys_.size() (%d < %d)", destSize, keys_.size()));
    }
  }

  const RowSequence &keys_;
  AnyChunk *const dest_;
};
}  // namespace

void ChunkFiller::fillChunk(const arrow::Array &src, const RowSequence &keys, AnyChunk *const dest) {
  Visitor visitor(keys, dest);
  okOrThrow(DEEPHAVEN_EXPR_MSG(src.Accept(&visitor)));
}
}  // namespace deephaven::client::chunk
