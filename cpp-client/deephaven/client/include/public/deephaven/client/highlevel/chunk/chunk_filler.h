#pragma once

#include <vector>
#include <arrow/array.h>
#include "deephaven/client/highlevel/column/column_source.h"
#include "deephaven/client/highlevel/container/row_sequence.h"
#include "deephaven/client/highlevel/chunk/chunk.h"

namespace deephaven::client::highlevel::chunk {
class ChunkFiller {
  typedef deephaven::client::highlevel::container::RowSequence RowSequence;
public:
  static void fillChunk(const arrow::Array &src, const RowSequence &keys, Chunk *dest);
};
}  // namespace deephaven::client::highlevel::chunk
