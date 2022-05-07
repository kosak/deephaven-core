#include "deephaven/client/highlevel/sad/sad_chunk.h"

#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::separatedList;
using deephaven::client::utility::stringf;

namespace deephaven::client::highlevel::sad {
std::shared_ptr<SadLongChunk> SadLongChunk::slice(size_t begin, size_t end) {
  if (begin > end) {
    auto message = stringf("begin (%o) > end (%o)", begin, end);
    throw std::runtime_error(message);
  }
  if (end > capacity()) {
    auto message = stringf("end (%o) > capacity (%o)", end, capacity());
    throw std::runtime_error(message);
  }
  return std::make_shared<SadLongChunk>(Private(), buffer_, begin, end);
}

std::ostream &operator<<(std::ostream &s, const SadLongChunk &o) {
  return s << '[' << separatedList(o.begin(), o.end()) << ']';
}
}  // namespace deephaven::client::highlevel::sad
