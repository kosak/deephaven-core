#pragma once
#include <immer/algorithm.hpp>
#include <immer/flex_vector.hpp>
#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::immerutil {
template<typename T>
class ImmerColumnSource final : public deephaven::client::column::NumericColumnSource<T>,
    std::enable_shared_from_this<ImmerColumnSource<T>> {
protected:
  typedef deephaven::client::chunk::Chunk Chunk;
  typedef deephaven::client::chunk::UInt64Chunk UInt64Chunk;
  typedef deephaven::client::column::ColumnSourceVisitor ColumnSourceVisitor;
  typedef deephaven::client::container::Context Context;
  typedef deephaven::client::container::RowSequence RowSequence;

public:
  explicit ImmerColumnSource(immer::flex_vector<T> data) : data_(std::move(data)) {}

  ~ImmerColumnSource() final = default;

  std::shared_ptr<Context> createContext(size_t chunkSize) const {
    return std::make_shared<Context>();
  }

  void fillChunkUnordered(Context *context, const UInt64Chunk &rowKeys, Chunk *dest) const final {
    using deephaven::client::utility::stringf;
    throw std::runtime_error(stringf("TODO(kosak): %o", DEEPHAVEN_PRETTY_FUNCTION));
  }

  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;

  void acceptVisitor(ColumnSourceVisitor *visitor) const final;

  std::any backdoor() const final {
    return &data_;
  }

private:
  immer::flex_vector<T> data_;
};

template<typename T>
void ImmerColumnSource<T>::fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const {
  using deephaven::client::chunk::TypeToChunk;
  using deephaven::client::utility::assertLessEq;
  using deephaven::client::utility::streamf;
  using deephaven::client::utility::verboseCast;

  typedef typename TypeToChunk<T>::type_t chunkType_t;

  auto *typedDest = verboseCast<chunkType_t*>(dest, DEEPHAVEN_PRETTY_FUNCTION);
  assertLessEq(rows.size(), typedDest->size(), DEEPHAVEN_PRETTY_FUNCTION, "rows.size()", "typedDest->size()");
  auto *destp = typedDest->data();

  auto copyDataInner = [&destp](const T *dataBegin, const T *dataEnd) {
    for (const T *current = dataBegin; current != dataEnd; ++current) {
      *destp++ = *current;
    }
  };

  auto copyDataOuter = [this, &copyDataInner](uint64_t srcBegin, uint64_t srcEnd) {
    auto srcBeginp = data_.begin() + srcBegin;
    auto srcEndp = data_.begin() + srcEnd;
    immer::for_each_chunk(srcBeginp, srcEndp, copyDataInner);
  };
  rows.forEachChunk(copyDataOuter);
}

template<typename T>
void ImmerColumnSource<T>::acceptVisitor(ColumnSourceVisitor *visitor) const {
  visitor->visit(*this);
}
}  // namespace deephaven::client::immerutil
