#include "deephaven/client/chunk/chunk.h"

#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::separatedList;
using deephaven::client::utility::stringf;

namespace deephaven::client::chunk {
namespace internal {
void checkSize(size_t proposedSize, size_t currentSize, std::string_view what) {
  if (proposedSize > currentSize) {
    auto message = stringf("%o: new size > size (%o > %o)", what, proposedSize, currentSize);
    throw std::runtime_error(message);
  }
}
}  // namespace internal
}  // namespace deephaven::client::chunk
