#pragma once
#include <immer/algorithm.hpp>
#include <immer/flex_vector.hpp>
#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::immerutil {
namespace internal {
// TODO(kosak): need to do this differently
template<typename T>
struct TypeToChunk {};

template<>
struct TypeToChunk<int32_t> {
  typedef deephaven::client::chunk::IntChunk type_t;
};

template<>
struct TypeToChunk<int64_t> {
  typedef deephaven::client::chunk::LongChunk type_t;
};

template<>
struct TypeToChunk<double> {
  typedef deephaven::client::chunk::DoubleChunk type_t;
};


}  // namespace internal
class ImmerColumnSourceBase : public deephaven::client::column::ColumnSource {
protected:

  typedef deephaven::client::column::ColumnSourceContext ColumnSourceContext;
public:
  std::shared_ptr<ColumnSourceContext> createContext(size_t chunkSize) const final;
  void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size,
      Chunk *dest) const final;
  void acceptVisitor(column::ColumnSourceVisitor *visitor) const final;
};

template<typename T>
class ImmerColumnSource final
    : public ImmerColumnSourceBase, std::enable_shared_from_this<ImmerColumnSource<T>> {
  typedef deephaven::client::column::ColumnSourceContext ColumnSourceContext;

public:
  explicit ImmerColumnSource(immer::flex_vector<T> data) : data_(std::move(data)) {}

  ~ImmerColumnSource() final = default;

  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;

private:
  immer::flex_vector<T> data_;
};

template<typename T>
void ImmerColumnSource<T>::fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const {
  if (rows.size() > dest->capacity()) {
    auto message = deephaven::client::utility::stringf(
        "rows.size() > dest->capacity() (%o > %o)", rows.size(), dest->capacity());
    throw std::runtime_error(message);
  }
  typedef typename internal::TypeToChunk<T>::type_t chunkType_t;
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
