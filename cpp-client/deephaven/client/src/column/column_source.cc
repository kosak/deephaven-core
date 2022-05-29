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
ColumnSource::~ColumnSource() = default;
MutableColumnSource::~MutableColumnSource() = default;
}  // namespace deephaven::client::column
