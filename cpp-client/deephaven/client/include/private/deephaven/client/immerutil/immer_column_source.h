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
  typedef deephaven::client::chunk::LongChunk LongChunk;
  typedef deephaven::client::column::ColumnSourceVisitor ColumnSourceVisitor;
  typedef deephaven::client::container::Context Context;
  typedef deephaven::client::container::RowSequence RowSequence;

public:
  explicit ImmerColumnSource(immer::flex_vector<T> data) : data_(std::move(data)) {}

  ~ImmerColumnSource() final = default;

  std::shared_ptr<Context> createContext(size_t chunkSize) const {
    return std::make_shared<Context>();
  }

  void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size,
      Chunk *dest) const {
    using deephaven::client::utility::stringf;
    throw std::runtime_error(stringf("TODO(kosak): %o", __PRETTY_FUNCTION__));
  }

  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;

  void acceptVisitor(ColumnSourceVisitor *visitor) const final;

private:
  immer::flex_vector<T> data_;
};

template<typename T>
void ImmerColumnSource<T>::fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const {
  using deephaven::client::chunk::TypeToChunk;
  using deephaven::client::utility::assertLessEq;
  using deephaven::client::utility::streamf;

  assertLessEq(rows.size(), dest->capacity(), __PRETTY_FUNCTION__, "rows.size()", "dest->capacity()");
  typedef typename TypeToChunk<T>::type_t chunkType_t;
  auto *typedDest = deephaven::client::utility::verboseCast<chunkType_t*>(__PRETTY_FUNCTION__ , dest);
  auto *destp = typedDest->data();

  auto copyDataInner = [&destp](const T *dataBegin, const T *dataEnd) {
    for (const T *current = dataBegin; current != dataEnd; ++current) {
      *destp = *current;
      ++destp;
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
  visitor->visit(this);
}
}  // namespace deephaven::client::immerutil
