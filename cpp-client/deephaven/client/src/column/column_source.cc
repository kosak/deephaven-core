#include "deephaven/client/column/column_source.h"

#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/container/context.h"
#include "deephaven/client/impl/util.h"

#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::streamf;
using deephaven::client::utility::stringf;
using deephaven::client::utility::verboseCast;
using deephaven::client::chunk::DoubleChunk;
using deephaven::client::chunk::IntChunk;

namespace deephaven::client::column {
namespace {
void assertFits(size_t size, size_t capacity);
void assertInRange(size_t index, size_t size);
}  // namespace
ColumnSource::~ColumnSource() = default;
MutableColumnSource::~MutableColumnSource() = default;


namespace {
void assertFits(size_t size, size_t capacity) {
  if (size > capacity) {
    auto message = stringf("Expected capacity at least %o, have %o", size, capacity);
    throw std::runtime_error(message);
  }
}

void assertInRange(size_t index, size_t size) {
  if (index >= size) {
    auto message = stringf("srcIndex %o >= size %o", index, size);
    throw std::runtime_error(message);
  }
}
}  // namespace
}  // namespace deephaven::client::column
