#pragma once

#include <cstdlib>
#include <flatbuffers/vector.h>
#include "deephaven/client/container/row_sequence.h"

namespace deephaven::client::subscription {
class DataInput {
public:
  explicit DataInput(const flatbuffers::Vector<int8_t> &vec) : DataInput(vec.data(), vec.size()) {}

  DataInput(const void *start, size_t size) : data_(static_cast<const char *>(start)),
      size_(size) {}

  int64_t readValue(int command);

  int64_t readLong();
  int32_t readInt();
  int16_t readShort();
  int8_t readByte();

private:
  const char *data_ = nullptr;
  size_t size_ = 0;
};

struct IndexDecoder {
  typedef deephaven::client::container::RowSequence RowSequence;

  static std::shared_ptr<RowSequence> readExternalCompressedDelta(DataInput *in);
};
}  // namespace deephaven::client::subscription
