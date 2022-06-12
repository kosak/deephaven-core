#include "deephaven/client/chunk/chunk.h"

#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::separatedList;
using deephaven::client::utility::stringf;

namespace deephaven::client::chunk {
void Chunk::checkSize(size_t proposedSize, std::string_view what) const {
  if (proposedSize > size_) {
    auto message = stringf("%o: new size > size (%o > %o)", what, proposedSize, size_);
    throw std::runtime_error(message);
  }
}
}  // namespace deephaven::client::chunk
