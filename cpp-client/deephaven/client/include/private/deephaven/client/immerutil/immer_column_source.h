#pragma once
#include "deephaven/client/column/column_source.h"

namespace deephaven::client::immerutil {
class ImmerColumnSourceBase : public deephaven::client::column::ColumnSource {
};

template<typename T>
class ImmerColumnSource final
    : public ImmerColumnSourceBase, std::enable_shared_from_this<ImmerColumnSource<T>> {
  typedef deephaven::client::column::ColumnSourceContext ColumnSourceContext;

public:
  explicit ImmerColumnSource(immer::flex_vector<T> data) : data_(std::move(data)) {}

  ~ImmerColumnSource() final = default;

  std::shared_ptr<ColumnSourceContext> createContext(size_t chunkSize) const final;
  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;
  void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size,
      Chunk *dest) const final;
  void acceptVisitor(column::ColumnSourceVisitor *visitor) const final;

private:
  immer::flex_vector<T> data_;
};
}  // namespace deephaven::client::immerutil
