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
  Visitor(const RowSequence &keys, Chunk *const dest) : keys_(keys), dest_(dest) {}

  arrow::Status Visit(const arrow::Int32Array &array) final;
  arrow::Status Visit(const arrow::Int64Array &array) final;
  arrow::Status Visit(const arrow::DoubleArray &array) final;

  const RowSequence &keys_;
  Chunk *const dest_;
  std::shared_ptr<Chunk> result_;
};
}  // namespace

void ChunkFiller::fillChunk(const arrow::Array &src, const RowSequence &keys, Chunk *const dest) {
  if (keys.size() < dest->size()) {
    auto message = stringf("keys.size() < dest->capacity() (%d < %d)", keys.size(), dest->size());
    throw std::runtime_error(message);
  }
  Visitor visitor(keys, dest);
  okOrThrow(DEEPHAVEN_EXPR_MSG(src.Accept(&visitor)));
}

namespace {
arrow::Status Visitor::Visit(const arrow::Int32Array &array) {
  auto *typedDest = verboseCast<IntChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest_);
  int64_t destIndex = 0;
  auto copyChunk = [&destIndex, &array, typedDest](uint64_t begin, uint64_t end) {
    for (auto current = begin; current != end; ++current) {
      auto val = array[(int64_t)current];
      if (!val.has_value()) {
        auto message = stringf("%o: Not handling nulls yet", __PRETTY_FUNCTION__);
        throw std::runtime_error(message);
      }
      typedDest->data()[destIndex++] = *val;
    }
  };
  keys_.forEachChunk(copyChunk);
  return arrow::Status::OK();
}

arrow::Status Visitor::Visit(const arrow::Int64Array &array) {
  auto *typedDest = verboseCast<LongChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest_);
  int64_t destIndex = 0;
  auto copyChunk = [&destIndex, &array, typedDest](uint64_t begin, uint64_t end) {
    for (auto current = begin; current != end; ++current) {
      auto val = array[(int64_t)current];
      if (!val.has_value()) {
        auto message = stringf("%o: Not handling nulls yet", __PRETTY_FUNCTION__);
        throw std::runtime_error(message);
      }
      typedDest->data()[destIndex++] = *val;
    }
  };
  keys_.forEachChunk(copyChunk);
  return arrow::Status::OK();
}

arrow::Status Visitor::Visit(const arrow::DoubleArray &array) {
  auto *typedDest = verboseCast<DoubleChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest_);
  int64_t destIndex = 0;
  auto copyChunk = [&destIndex, &array, typedDest](uint64_t begin, uint64_t end) {
    for (auto current = begin; current != end; ++current) {
      auto val = array[(int64_t)current];
      if (!val.has_value()) {
        auto message = stringf("%o: Not handling nulls yet", __PRETTY_FUNCTION__);
        throw std::runtime_error(message);
      }
      typedDest->data()[destIndex++] = *val;
    }
  };
  keys_.forEachChunk(copyChunk);
  return arrow::Status::OK();
}
}  // namespace
}  // namespace deephaven::client::chunk

