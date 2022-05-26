#include "deephaven/client/immerutil/immer_column_source.h"

#include "deephaven/client/utility/utility.h"
using deephaven::client::utility::stringf;

namespace deephaven::client::immerutil {
auto ImmerColumnSourceBase::createContext(size_t chunkSize) const -> std::shared_ptr<ColumnSourceContext> {
  throw std::runtime_error(stringf("TODO(kosak): %o", __PRETTY_FUNCTION__));
}

void ImmerColumnSourceBase::fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size,
    Chunk *dest) const {
  throw std::runtime_error(stringf("TODO(kosak): %o", __PRETTY_FUNCTION__));
}

void ImmerColumnSourceBase::acceptVisitor(column::ColumnSourceVisitor *visitor) const {
  throw std::runtime_error(stringf("TODO(kosak): %o", __PRETTY_FUNCTION__));
}
}  // namespace deephaven::client::immerutil
