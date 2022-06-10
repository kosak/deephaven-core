#include "deephaven/client/chunk/chunk.h"

#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::separatedList;
using deephaven::client::utility::stringf;

namespace deephaven::client::chunk {
void Chunk::checkSize(std::string_view what, size_t size) const {
  if (size > size_) {
    auto message = stringf("%o: new size > size (%o > %o)", size, size_);
    throw std::runtime_error(message);
  }
}

std::ostream &operator<<(std::ostream &s, const LongChunk &o) {
  return s << '[' << separatedList(o.begin(), o.end()) << ']';
}
}  // namespace deephaven::client::chunk
