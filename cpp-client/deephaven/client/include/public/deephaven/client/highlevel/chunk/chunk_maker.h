#pragma once

#include <vector>
#include <arrow/array.h>
#include "deephaven/client/highlevel/chunk/chunk.h"


namespace deephaven::client::highlevel::column {
class ColumnSource;
}  // namespace deephaven::client::highlevel::column

namespace deephaven::client::highlevel::chunk {
class ChunkMaker {
  typedef deephaven::client::highlevel::column::ColumnSource ColumnSource;
public:
  static std::shared_ptr<Chunk> createChunkFor(const ColumnSource &columnSource, size_t chunkSize);
};
}  // namespace deephaven::client::highlevel::chunk
