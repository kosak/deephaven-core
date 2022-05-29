#pragma once
#include <immer/algorithm.hpp>
#include <immer/flex_vector.hpp>
#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::immerutil {
class ImmerColumnSourceBase : public deephaven::client::column::ColumnSource {
public:
  std::shared_ptr<Context> createContext(size_t chunkSize) const final;
  void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size,
      Chunk *dest) const final;
  void acceptVisitor(column::ColumnSourceVisitor *visitor) const final;
};

template<typename T>
class ImmerColumnSource final
    : public ImmerColumnSourceBase, std::enable_shared_from_this<ImmerColumnSource<T>> {
public:
  explicit ImmerColumnSource(immer::flex_vector<T> data) : data_(std::move(data)) {}

  ~ImmerColumnSource() final = default;

  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;

private:
  immer::flex_vector<T> data_;
};

template<typename T>
void ImmerColumnSource<T>::fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const {
  using deephaven::client::chunk::TypeToChunk;
  using deephaven::client::utility::assertLessEq;

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
}  // namespace deephaven::client::immerutil
